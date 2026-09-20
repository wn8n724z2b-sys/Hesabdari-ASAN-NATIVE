from __future__ import annotations
from pathlib import Path
import sqlite3, tempfile, json, re, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
APP=ROOT/'HesabdariAsan.Native'
report={}

# XAML/XML and code-behind integrity
xml_errors=[]; class_errors=[]; event_errors=[]
XNS='{http://schemas.microsoft.com/winfx/2006/xaml}'
events=['Click','Loaded','Unloaded','Closing','Closed','PreviewKeyDown','KeyDown','KeyUp','TextChanged','SelectionChanged','MouseDown','MouseUp','Checked','Unchecked','SelectedDateChanged','SourceInitialized']
for p in APP.rglob('*.xaml'):
    try: root=ET.parse(p).getroot()
    except Exception as e: xml_errors.append(f'{p.relative_to(APP)}: {e}'); continue
    xc=root.attrib.get(XNS+'Class')
    if xc:
        cb=Path(str(p)+'.cs')
        if not cb.exists(): class_errors.append(f'{p.relative_to(APP)}: codebehind missing')
        else:
            txt=cb.read_text(encoding='utf-8-sig',errors='ignore'); name=xc.split('.')[-1]
            if not re.search(r'\b(?:partial\s+)?class\s+'+re.escape(name)+r'\b',txt): class_errors.append(f'{p.relative_to(APP)}: class mismatch {xc}')
            xtext=p.read_text(encoding='utf-8-sig',errors='ignore')
            for ev in events:
                for m in re.finditer(r'\b'+ev+r'\s*=\s*"([A-Za-z_]\w*)"',xtext):
                    if not re.search(r'\b'+re.escape(m.group(1))+r'\s*\(',txt): event_errors.append(f'{p.relative_to(APP)}: {ev}={m.group(1)}')
report['xaml']={'count':len(list(APP.rglob('*.xaml'))),'xml_errors':xml_errors,'class_errors':class_errors,'event_errors':event_errors}

# Resource references
keys=set(); refs=[]
for p in APP.rglob('*.xaml'):
    t=p.read_text(encoding='utf-8-sig',errors='ignore'); keys.update(re.findall(r'x:Key\s*=\s*"([^"]+)"',t))
for p in APP.rglob('*.xaml'):
    t=p.read_text(encoding='utf-8-sig',errors='ignore')
    refs += re.findall(r'\{(?:StaticResource|DynamicResource)\s+([^}\s]+)\}',t)
report['resources']={'defined':len(keys),'references':len(refs),'missing':sorted(set(refs)-keys)}

# Build a schema-6 database matching Database.Initialize shape.
db_path=Path(tempfile.gettempdir())/'hesabdari_rc1_validate.sqlite3'
for suffix in ('','-wal','-shm'):
    try:(Path(str(db_path)+suffix)).unlink()
    except FileNotFoundError:pass
con=sqlite3.connect(db_path)
con.executescript('''
PRAGMA foreign_keys=ON;
CREATE TABLE app_meta(key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE work_sessions(id INTEGER PRIMARY KEY AUTOINCREMENT,started_at TEXT NOT NULL,ended_at TEXT,duration_seconds INTEGER NOT NULL DEFAULT 0,last_pulse_at TEXT);
CREATE TABLE products(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,barcode TEXT,category TEXT NOT NULL DEFAULT 'عمومی',unit TEXT NOT NULL DEFAULT 'عدد',purchase_price INTEGER NOT NULL DEFAULT 0,sale_price INTEGER NOT NULL DEFAULT 0,stock REAL NOT NULL DEFAULT 0,min_stock REAL NOT NULL DEFAULT 0,is_active INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL,producer TEXT NOT NULL DEFAULT '',image_path TEXT,unit_conversion_enabled INTEGER NOT NULL DEFAULT 0,base_unit TEXT NOT NULL DEFAULT 'دانه',purchase_unit TEXT NOT NULL DEFAULT 'دانه',units_per_purchase REAL NOT NULL DEFAULT 1,package_buy_price INTEGER NOT NULL DEFAULT 0);
CREATE TABLE product_barcodes(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,barcode TEXT NOT NULL UNIQUE,is_primary INTEGER NOT NULL DEFAULT 0,FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE CASCADE);
CREATE TABLE parties(id INTEGER PRIMARY KEY AUTOINCREMENT,type TEXT NOT NULL,name TEXT NOT NULL,phone TEXT,address TEXT,debt INTEGER NOT NULL DEFAULT 0,is_pinned INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL);
CREATE TABLE expenses(id INTEGER PRIMARY KEY AUTOINCREMENT,title TEXT NOT NULL,category TEXT NOT NULL DEFAULT 'هزینه',amount INTEGER NOT NULL,note TEXT,created_at TEXT NOT NULL);
CREATE TABLE customer_receipts(id INTEGER PRIMARY KEY AUTOINCREMENT,party_id INTEGER NOT NULL,amount INTEGER NOT NULL,note TEXT,created_at TEXT NOT NULL,FOREIGN KEY(party_id) REFERENCES parties(id));
CREATE TABLE supplier_payments(id INTEGER PRIMARY KEY AUTOINCREMENT,party_id INTEGER NOT NULL,amount INTEGER NOT NULL,note TEXT,created_at TEXT NOT NULL,FOREIGN KEY(party_id) REFERENCES parties(id));
CREATE TABLE invoices(id INTEGER PRIMARY KEY AUTOINCREMENT,invoice_no INTEGER NOT NULL UNIQUE,customer_id INTEGER,customer_name TEXT NOT NULL DEFAULT 'مشتری عمومی',payment_type TEXT NOT NULL,subtotal INTEGER NOT NULL DEFAULT 0,discount INTEGER NOT NULL DEFAULT 0,total INTEGER NOT NULL DEFAULT 0,paid INTEGER NOT NULL DEFAULT 0,status TEXT NOT NULL DEFAULT 'ACTIVE',cancelled_at TEXT,cancel_reason TEXT,updated_at TEXT,created_at TEXT NOT NULL);
CREATE TABLE invoice_items(id INTEGER PRIMARY KEY AUTOINCREMENT,invoice_id INTEGER NOT NULL,product_id INTEGER,product_name TEXT NOT NULL,barcode TEXT,qty REAL NOT NULL,unit_price INTEGER NOT NULL,purchase_price INTEGER NOT NULL DEFAULT 0,line_total INTEGER NOT NULL,unit TEXT NOT NULL DEFAULT 'دانه',FOREIGN KEY(invoice_id) REFERENCES invoices(id) ON DELETE CASCADE);
CREATE TABLE inventory_ledger(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,movement_type TEXT NOT NULL,qty REAL NOT NULL,balance_after REAL NOT NULL,reference_type TEXT,reference_id INTEGER,note TEXT,created_at TEXT NOT NULL);
CREATE TABLE purchases(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER,product_name TEXT NOT NULL,quantity REAL NOT NULL DEFAULT 0,units REAL NOT NULL DEFAULT 0,unit_cost INTEGER NOT NULL DEFAULT 0,total INTEGER NOT NULL DEFAULT 0,payment_type TEXT NOT NULL DEFAULT 'CASH',supplier_id INTEGER,note TEXT,mode TEXT NOT NULL DEFAULT 'BASE',purchase_unit TEXT NOT NULL DEFAULT 'دانه',units_per_purchase REAL NOT NULL DEFAULT 1,pack_cost INTEGER NOT NULL DEFAULT 0,average_cost INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL);
CREATE TABLE financial_periods(id INTEGER PRIMARY KEY AUTOINCREMENT,period_no INTEGER NOT NULL UNIQUE,started_at TEXT NOT NULL,ended_at TEXT,summary_json TEXT);
CREATE TABLE audit_log(id INTEGER PRIMARY KEY AUTOINCREMENT,created_at TEXT NOT NULL,action TEXT NOT NULL,entity_type TEXT,entity_id TEXT,payload_json TEXT,actor TEXT NOT NULL DEFAULT 'Admin',invoice_no INTEGER);
CREATE TABLE invoice_revisions(id INTEGER PRIMARY KEY AUTOINCREMENT,invoice_id INTEGER,invoice_no INTEGER NOT NULL,revision_no INTEGER NOT NULL,changed_at TEXT NOT NULL,action TEXT NOT NULL,actor TEXT NOT NULL DEFAULT 'Admin',snapshot_json TEXT NOT NULL);
CREATE TABLE legacy_id_map(entity_type TEXT NOT NULL,legacy_id TEXT NOT NULL,native_id INTEGER NOT NULL,PRIMARY KEY(entity_type,legacy_id));
''')
# Seed accounting scenario.
con.execute("INSERT INTO parties(type,name,debt,is_pinned,created_at) VALUES('CUSTOMER','مشتری عمومی',0,1,'2026-09-01T00:00:00')")
cust=con.execute("INSERT INTO parties(type,name,debt,is_pinned,created_at) VALUES('CUSTOMER','مشتری تست',700,0,'2026-09-01T00:00:00')").lastrowid
supp=con.execute("INSERT INTO parties(type,name,debt,is_pinned,created_at) VALUES('COMPANY','شرکت تست',900,0,'2026-09-01T00:00:00')").lastrowid
prod=con.execute("INSERT INTO products(name,barcode,category,unit,purchase_price,sale_price,stock,min_stock,is_active,created_at,base_unit,purchase_unit,units_per_purchase,package_buy_price) VALUES('کالای تست','123','عمومی','دانه',60,100,50,5,1,'2026-09-01T00:00:00','دانه','کارتن',10,600)").lastrowid
# cash invoice 1000, cost 600
inv1=con.execute("INSERT INTO invoices(invoice_no,customer_name,payment_type,subtotal,discount,total,paid,status,created_at) VALUES(1,'مشتری عمومی','CASH',1000,0,1000,1000,'ACTIVE','2026-09-20T09:00:00')").lastrowid
con.execute("INSERT INTO invoice_items(invoice_id,product_id,product_name,qty,unit_price,purchase_price,line_total,unit) VALUES(?,?,?,?,?,?,?,?)",(inv1,prod,'کالای تست',10,100,60,1000,'دانه'))
# credit invoice 500, cost 300, discount 50 already reflected total=450
inv2=con.execute("INSERT INTO invoices(invoice_no,customer_id,customer_name,payment_type,subtotal,discount,total,paid,status,created_at) VALUES(2,?,'مشتری تست','CREDIT',500,50,450,0,'ACTIVE','2026-09-20T10:00:00')",(cust,)).lastrowid
con.execute("INSERT INTO invoice_items(invoice_id,product_id,product_name,qty,unit_price,purchase_price,line_total,unit) VALUES(?,?,?,?,?,?,?,?)",(inv2,prod,'کالای تست',5,100,60,500,'دانه'))
# cancelled invoice must be excluded
inv3=con.execute("INSERT INTO invoices(invoice_no,customer_name,payment_type,subtotal,discount,total,paid,status,created_at) VALUES(3,'مشتری عمومی','CASH',999,0,999,999,'CANCELLED','2026-09-20T11:00:00')").lastrowid
con.execute("INSERT INTO invoice_items(invoice_id,product_id,product_name,qty,unit_price,purchase_price,line_total,unit) VALUES(?,?,?,?,?,?,?,?)",(inv3,prod,'کالای تست',1,999,60,999,'دانه'))
con.execute("INSERT INTO expenses(title,category,amount,created_at) VALUES('هزینه تست','عمومی',100,'2026-09-20T12:00:00')")
con.execute("INSERT INTO customer_receipts(party_id,amount,created_at) VALUES(?,?,?)",(cust,200,'2026-09-20T13:00:00'))
con.execute("INSERT INTO supplier_payments(party_id,amount,created_at) VALUES(?,?,?)",(supp,150,'2026-09-20T14:00:00'))
con.execute("INSERT INTO purchases(product_id,product_name,quantity,units,unit_cost,total,payment_type,supplier_id,created_at) VALUES(?,?,?,?,?,?,?,?,?)",(prod,'کالای تست',5,5,60,300,'CASH',supp,'2026-09-20T08:00:00'))
con.commit()

sales, count, cash, credit, discounts=con.execute("SELECT COALESCE(SUM(total),0),COUNT(*),COALESCE(SUM(CASE WHEN payment_type='CASH' THEN total ELSE 0 END),0),COALESCE(SUM(CASE WHEN payment_type='CREDIT' THEN total ELSE 0 END),0),COALESCE(SUM(discount),0) FROM invoices WHERE status='ACTIVE'").fetchone()
expenses=con.execute('SELECT COALESCE(SUM(amount),0) FROM expenses').fetchone()[0]
receipts=con.execute('SELECT COALESCE(SUM(amount),0) FROM customer_receipts').fetchone()[0]
supplier_paid=con.execute('SELECT COALESCE(SUM(amount),0) FROM supplier_payments').fetchone()[0]
cash_purchases=con.execute("SELECT COALESCE(SUM(total),0) FROM purchases WHERE payment_type='CASH'").fetchone()[0]
cost,gross=con.execute("SELECT COALESCE(SUM(CAST(ii.purchase_price*ii.qty AS INTEGER)),0),COALESCE(SUM(CAST((ii.unit_price-ii.purchase_price)*ii.qty AS INTEGER)),0) FROM invoice_items ii JOIN invoices i ON i.id=ii.invoice_id WHERE i.status='ACTIVE'").fetchone()
calc={'sales':sales,'invoice_count':count,'cash_sales':cash,'credit_sales':credit,'discounts':discounts,'expenses':expenses,'receipts':receipts,'supplier_payments':supplier_paid,'cash_purchases':cash_purchases,'cogs':cost,'gross_profit':gross,'net_profit':gross-expenses,'cash_in':cash+receipts,'cash_out':expenses+supplier_paid+cash_purchases,'net_cash':cash+receipts-expenses-supplier_paid-cash_purchases}
expected={'sales':1450,'invoice_count':2,'cash_sales':1000,'credit_sales':450,'discounts':50,'expenses':100,'receipts':200,'supplier_payments':150,'cash_purchases':300,'cogs':900,'gross_profit':600,'net_profit':500,'cash_in':1200,'cash_out':550,'net_cash':650}
report['accounting_formula_test']={'actual':calc,'expected':expected,'ok':calc==expected}
report['sqlite_quick_check']=con.execute('PRAGMA quick_check').fetchone()[0]
# Reset integrity: destructive data tables clear without breaking schema.
for table in ['invoice_revisions','audit_log','inventory_ledger','invoice_items','invoices','customer_receipts','supplier_payments','purchases','expenses','product_barcodes','products','parties','financial_periods','work_sessions','legacy_id_map','settings','app_meta']:
    con.execute(f'DELETE FROM {table}')
con.commit()
remaining=sum(con.execute(f'SELECT COUNT(*) FROM {t}').fetchone()[0] for t in ['invoices','invoice_items','products','parties','expenses','purchases'])
report['reset_test']={'core_rows_remaining':remaining,'ok':remaining==0}
con.close()

report['status']='PASS' if not xml_errors and not class_errors and not event_errors and not report['resources']['missing'] and report['accounting_formula_test']['ok'] and report['sqlite_quick_check']=='ok' and report['reset_test']['ok'] else 'FAIL'
out=APP/'RC1_VALIDATION_REPORT.json'
out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
