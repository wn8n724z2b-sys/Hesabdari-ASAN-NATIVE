#!/usr/bin/env python3
import os, sqlite3, tempfile, time, json, random
from datetime import datetime, timedelta

N_PRODUCTS=5000
N_INVOICES=12000
ITEMS_PER_INVOICE=3
N_EXPENSES=3500
N_PURCHASES=2500

def ms(t): return (time.perf_counter()-t)*1000

def schema_v4(c):
    c.executescript('''
PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;
CREATE TABLE products(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,barcode TEXT,category TEXT NOT NULL DEFAULT 'عمومی',unit TEXT NOT NULL DEFAULT 'عدد',purchase_price INTEGER NOT NULL DEFAULT 0,sale_price INTEGER NOT NULL DEFAULT 0,stock REAL NOT NULL DEFAULT 0,min_stock REAL NOT NULL DEFAULT 0,is_active INTEGER NOT NULL DEFAULT 1,created_at TEXT NOT NULL);
CREATE INDEX idx_products_name ON products(name); CREATE INDEX idx_products_barcode ON products(barcode); CREATE INDEX idx_products_category ON products(category);
CREATE TABLE parties(id INTEGER PRIMARY KEY AUTOINCREMENT,type TEXT NOT NULL,name TEXT NOT NULL,phone TEXT,address TEXT,debt INTEGER NOT NULL DEFAULT 0,is_pinned INTEGER NOT NULL DEFAULT 0,created_at TEXT NOT NULL);
CREATE INDEX idx_parties_type ON parties(type); CREATE INDEX idx_parties_name ON parties(name);
CREATE TABLE invoices(id INTEGER PRIMARY KEY AUTOINCREMENT,invoice_no INTEGER NOT NULL UNIQUE,customer_id INTEGER,customer_name TEXT NOT NULL DEFAULT 'مشتری عمومی',payment_type TEXT NOT NULL,subtotal INTEGER NOT NULL DEFAULT 0,discount INTEGER NOT NULL DEFAULT 0,total INTEGER NOT NULL DEFAULT 0,paid INTEGER NOT NULL DEFAULT 0,status TEXT NOT NULL DEFAULT 'ACTIVE',cancelled_at TEXT,cancel_reason TEXT,updated_at TEXT,created_at TEXT NOT NULL,FOREIGN KEY(customer_id) REFERENCES parties(id) ON DELETE SET NULL);
CREATE INDEX idx_invoices_created_at ON invoices(created_at); CREATE INDEX idx_invoices_customer_id ON invoices(customer_id); CREATE INDEX idx_invoices_status ON invoices(status);
CREATE TABLE invoice_items(id INTEGER PRIMARY KEY AUTOINCREMENT,invoice_id INTEGER NOT NULL,product_id INTEGER,product_name TEXT NOT NULL,barcode TEXT,qty REAL NOT NULL,unit_price INTEGER NOT NULL,purchase_price INTEGER NOT NULL DEFAULT 0,line_total INTEGER NOT NULL,FOREIGN KEY(invoice_id) REFERENCES invoices(id) ON DELETE CASCADE,FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE SET NULL);
CREATE INDEX idx_invoice_items_invoice ON invoice_items(invoice_id);
CREATE TABLE inventory_ledger(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,movement_type TEXT NOT NULL,qty REAL NOT NULL,balance_after REAL NOT NULL,reference_type TEXT,reference_id INTEGER,note TEXT,created_at TEXT NOT NULL,FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE CASCADE);
CREATE INDEX idx_inventory_product ON inventory_ledger(product_id); CREATE INDEX idx_inventory_created_at ON inventory_ledger(created_at);
CREATE TABLE expenses(id INTEGER PRIMARY KEY AUTOINCREMENT,title TEXT NOT NULL,category TEXT NOT NULL DEFAULT 'هزینه',amount INTEGER NOT NULL,note TEXT,created_at TEXT NOT NULL); CREATE INDEX idx_expenses_created_at ON expenses(created_at);
CREATE TABLE customer_receipts(id INTEGER PRIMARY KEY AUTOINCREMENT,party_id INTEGER NOT NULL,amount INTEGER NOT NULL,note TEXT,created_at TEXT NOT NULL,FOREIGN KEY(party_id) REFERENCES parties(id) ON DELETE CASCADE);
CREATE TABLE supplier_payments(id INTEGER PRIMARY KEY AUTOINCREMENT,party_id INTEGER NOT NULL,amount INTEGER NOT NULL,note TEXT,created_at TEXT NOT NULL,FOREIGN KEY(party_id) REFERENCES parties(id) ON DELETE CASCADE);
CREATE TABLE purchases(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER,product_name TEXT NOT NULL,quantity REAL NOT NULL DEFAULT 0,units REAL NOT NULL DEFAULT 0,unit_cost INTEGER NOT NULL DEFAULT 0,total INTEGER NOT NULL DEFAULT 0,payment_type TEXT NOT NULL DEFAULT 'CASH',supplier_id INTEGER,note TEXT,created_at TEXT NOT NULL,FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE SET NULL,FOREIGN KEY(supplier_id) REFERENCES parties(id) ON DELETE SET NULL);
CREATE INDEX idx_purchases_created_at ON purchases(created_at); CREATE INDEX idx_purchases_product ON purchases(product_id); CREATE INDEX idx_purchases_supplier ON purchases(supplier_id);
CREATE TABLE audit_log(id INTEGER PRIMARY KEY AUTOINCREMENT,created_at TEXT NOT NULL,action TEXT NOT NULL,entity_type TEXT,entity_id TEXT,payload_json TEXT,actor TEXT NOT NULL DEFAULT 'Admin',invoice_no INTEGER); CREATE INDEX idx_audit_created_at ON audit_log(created_at); CREATE INDEX idx_audit_invoice_no ON audit_log(invoice_no); CREATE INDEX idx_audit_entity ON audit_log(entity_type,entity_id);
CREATE TABLE invoice_revisions(id INTEGER PRIMARY KEY AUTOINCREMENT,invoice_id INTEGER,invoice_no INTEGER NOT NULL,revision_no INTEGER NOT NULL,changed_at TEXT NOT NULL,action TEXT NOT NULL,actor TEXT NOT NULL DEFAULT 'Admin',snapshot_json TEXT NOT NULL,FOREIGN KEY(invoice_id) REFERENCES invoices(id) ON DELETE SET NULL);
''')

def migration_test():
    db=sqlite3.connect(':memory:')
    db.executescript('''
    CREATE TABLE invoices(id INTEGER PRIMARY KEY, invoice_no INTEGER UNIQUE, customer_id INTEGER, customer_name TEXT, payment_type TEXT, subtotal INTEGER, discount INTEGER,total INTEGER,paid INTEGER,created_at TEXT);
    CREATE TABLE purchases(id INTEGER PRIMARY KEY, product_id INTEGER, product_name TEXT, quantity REAL, units REAL, unit_cost INTEGER,total INTEGER,payment_type TEXT,supplier_id INTEGER,created_at TEXT);
    ''')
    def ensure(table,col,definition):
        cols={r[1] for r in db.execute(f'PRAGMA table_info({table})')}
        if col not in cols: db.execute(f'ALTER TABLE {table} ADD COLUMN {col} {definition}')
    ensure('invoices','status',"TEXT NOT NULL DEFAULT 'ACTIVE'")
    ensure('invoices','cancelled_at','TEXT NULL')
    ensure('invoices','cancel_reason','TEXT NULL')
    ensure('invoices','updated_at','TEXT NULL')
    ensure('purchases','note','TEXT NULL')
    db.execute('CREATE INDEX IF NOT EXISTS idx_invoices_status ON invoices(status)')
    cols={r[1] for r in db.execute('PRAGMA table_info(invoices)')}
    assert {'status','cancelled_at','cancel_reason','updated_at'} <= cols
    assert 'note' in {r[1] for r in db.execute('PRAGMA table_info(purchases)')}
    return True

def run():
    random.seed(7)
    path=os.path.join(tempfile.gettempdir(),'hesabdari_asan_v4_phase4_stress.sqlite3')
    try: os.remove(path)
    except FileNotFoundError: pass
    db=sqlite3.connect(path)
    db.execute('PRAGMA foreign_keys=ON')
    schema_v4(db)
    now=datetime.now()

    t=time.perf_counter()
    db.executemany('INSERT INTO products(name,barcode,category,unit,purchase_price,sale_price,stock,min_stock,is_active,created_at) VALUES(?,?,?,?,?,?,?,?,1,?)',[
        (f'کالا {i:05d}',f'{1000000000000+i}','عمومی','عدد',50+(i%200),100+(i%500),100000.0,5.0,now.isoformat()) for i in range(1,N_PRODUCTS+1)
    ])
    product_insert_ms=ms(t)

    parties=[('CUSTOMER',f'مشتری {i:04d}',f'07{i:08d}','',0,0,now.isoformat()) for i in range(1,801)]
    parties += [('COMPANY',f'شرکت {i:03d}',f'08{i:08d}','',0,0,now.isoformat()) for i in range(1,201)]
    db.executemany('INSERT INTO parties(type,name,phone,address,debt,is_pinned,created_at) VALUES(?,?,?,?,?,?,?)',parties)
    db.commit()

    t=time.perf_counter()
    cur=db.cursor()
    for ino in range(1,N_INVOICES+1):
        dt=(now-timedelta(days=ino%120,minutes=ino%1440)).isoformat()
        cid=1+(ino%800)
        credit=(ino%4==0)
        line_specs=[]; subtotal=0
        for j in range(ITEMS_PER_INVOICE):
            pid=1+((ino*ITEMS_PER_INVOICE+j)%N_PRODUCTS)
            qty=1+(j%2); pp=80+(pid%150); sp=pp+50+(pid%100); lt=qty*sp; subtotal+=lt
            line_specs.append((pid,qty,pp,sp,lt))
        total=subtotal
        cur.execute('INSERT INTO invoices(invoice_no,customer_id,customer_name,payment_type,subtotal,discount,total,paid,status,created_at) VALUES(?,?,?,?,?,?,?,?,?,?)',(ino,cid,f'مشتری {cid:04d}','CREDIT' if credit else 'CASH',subtotal,0,total,0 if credit else total,'ACTIVE',dt))
        iid=cur.lastrowid
        for pid,qty,pp,sp,lt in line_specs:
            cur.execute('INSERT INTO invoice_items(invoice_id,product_id,product_name,barcode,qty,unit_price,purchase_price,line_total) VALUES(?,?,?,?,?,?,?,?)',(iid,pid,f'کالا {pid:05d}',str(1000000000000+pid),qty,sp,pp,lt))
            cur.execute('UPDATE products SET stock=stock-? WHERE id=?',(qty,pid))
        if credit: cur.execute('UPDATE parties SET debt=debt+? WHERE id=?',(total,cid))
        if ino%1000==0: db.commit()
    db.commit(); invoice_insert_ms=ms(t)

    db.executemany('INSERT INTO expenses(title,category,amount,note,created_at) VALUES(?,?,?,?,?)',[(f'هزینه {i}','عمومی',100+(i%4000),'', (now-timedelta(days=i%90)).isoformat()) for i in range(N_EXPENSES)])
    db.executemany('INSERT INTO purchases(product_id,product_name,quantity,units,unit_cost,total,payment_type,supplier_id,note,created_at) VALUES(?,?,?,?,?,?,?,?,?,?)',[(1+(i%N_PRODUCTS),f'کالا {1+(i%N_PRODUCTS):05d}',10,10,70,700,'CASH',801+(i%200),'تست',(now-timedelta(days=i%90)).isoformat()) for i in range(N_PURCHASES)])
    db.commit()

    timings={}
    for name,sql,args in [
        ('product_page_10',"SELECT id,name,barcode FROM products WHERE is_active=1 AND (name LIKE ? OR barcode LIKE ?) ORDER BY id DESC LIMIT 10 OFFSET 2000",('%کالا%','%کالا%')),
        ('invoice_page_10',"SELECT id,invoice_no,customer_name,total FROM invoices ORDER BY created_at DESC,id DESC LIMIT 10 OFFSET 8000",()),
        ('dashboard_today',"SELECT COALESCE(SUM(total),0) FROM invoices WHERE status='ACTIVE' AND created_at>=?",(datetime.today().isoformat(),)),
        ('trend_30d',"SELECT substr(created_at,1,10),SUM(total) FROM invoices WHERE status='ACTIVE' AND created_at>=? GROUP BY substr(created_at,1,10)",((now-timedelta(days=29)).isoformat(),)),
        ('invoice_profit',"SELECT COALESCE(SUM(CAST((ii.unit_price-ii.purchase_price)*ii.qty AS INTEGER)),0) FROM invoice_items ii JOIN invoices i ON i.id=ii.invoice_id WHERE i.status='ACTIVE' AND i.created_at>=?",((now-timedelta(days=30)).isoformat(),)),
    ]:
        t=time.perf_counter(); rows=list(db.execute(sql,args)); timings[name]=round(ms(t),3); assert rows is not None

    # purchase transaction invariant
    product_id=2; supplier_id=801
    stock_before=db.execute('SELECT stock FROM products WHERE id=?',(product_id,)).fetchone()[0]
    debt_before=db.execute('SELECT debt FROM parties WHERE id=?',(supplier_id,)).fetchone()[0]
    qty=17; cost=123; total=qty*cost
    with db:
        cur=db.execute("INSERT INTO purchases(product_id,product_name,quantity,units,unit_cost,total,payment_type,supplier_id,note,created_at) VALUES(?,?,?,?,?,?,?,?,?,?)",(product_id,'کالا 00002',qty,qty,cost,total,'CREDIT',supplier_id,'invariant',now.isoformat()))
        rid=cur.lastrowid
        db.execute('UPDATE products SET stock=stock+?,purchase_price=? WHERE id=?',(qty,cost,product_id))
        bal=db.execute('SELECT stock FROM products WHERE id=?',(product_id,)).fetchone()[0]
        db.execute("INSERT INTO inventory_ledger(product_id,movement_type,qty,balance_after,reference_type,reference_id,note,created_at) VALUES(?,?,?,?,?,?,?,?)",(product_id,'PURCHASE',qty,bal,'PURCHASE',rid,'invariant',now.isoformat()))
        db.execute('UPDATE parties SET debt=debt+? WHERE id=?',(total,supplier_id))
    assert abs(db.execute('SELECT stock FROM products WHERE id=?',(product_id,)).fetchone()[0]-(stock_before+qty))<1e-9
    assert db.execute('SELECT debt FROM parties WHERE id=?',(supplier_id,)).fetchone()[0]==debt_before+total

    # cancellation invariant on a cash invoice: restore inventory and exclude from active totals
    ino=1
    iid,status=db.execute('SELECT id,status FROM invoices WHERE invoice_no=?',(ino,)).fetchone(); assert status=='ACTIVE'
    lines=list(db.execute('SELECT product_id,qty FROM invoice_items WHERE invoice_id=?',(iid,)))
    stocks_before={pid:db.execute('SELECT stock FROM products WHERE id=?',(pid,)).fetchone()[0] for pid,_ in lines}
    with db:
        for pid,q in lines: db.execute('UPDATE products SET stock=stock+? WHERE id=?',(q,pid))
        db.execute("UPDATE invoices SET status='CANCELLED',cancelled_at=?,cancel_reason='stress-test' WHERE id=?",(now.isoformat(),iid))
    assert db.execute('SELECT status FROM invoices WHERE id=?',(iid,)).fetchone()[0]=='CANCELLED'
    for pid,q in lines: assert abs(db.execute('SELECT stock FROM products WHERE id=?',(pid,)).fetchone()[0]-(stocks_before[pid]+q))<1e-9

    # edit invariant on a cash invoice: reverse old stock, replace lines, deduct new stock atomically
    edit_ino=2
    edit_id=db.execute('SELECT id FROM invoices WHERE invoice_no=?',(edit_ino,)).fetchone()[0]
    old=list(db.execute('SELECT product_id,qty,unit_price,purchase_price FROM invoice_items WHERE invoice_id=? ORDER BY id',(edit_id,)))
    stock0={pid:db.execute('SELECT stock FROM products WHERE id=?',(pid,)).fetchone()[0] for pid,_,_,_ in old}
    new=[]
    for idx,(pid,q,sp,pp) in enumerate(old):
        newq=q+1 if idx==0 else q
        new.append((pid,newq,sp+5,pp))
    with db:
        for pid,q,_,_ in old: db.execute('UPDATE products SET stock=stock+? WHERE id=?',(q,pid))
        db.execute('DELETE FROM invoice_items WHERE invoice_id=?',(edit_id,))
        subtotal=0
        for pid,q,sp,pp in new:
            lt=round(q*sp); subtotal+=lt
            db.execute('INSERT INTO invoice_items(invoice_id,product_id,product_name,barcode,qty,unit_price,purchase_price,line_total) VALUES(?,?,?,?,?,?,?,?)',(edit_id,pid,f'کالا {pid:05d}',str(1000000000000+pid),q,sp,pp,lt))
            db.execute('UPDATE products SET stock=stock-? WHERE id=?',(q,pid))
        db.execute('UPDATE invoices SET subtotal=?,total=?,paid=?,updated_at=? WHERE id=?',(subtotal,subtotal,subtotal,now.isoformat(),edit_id))
    for pid,oldq,_,_ in old:
        newq=next(q for p,q,_,_ in new if p==pid)
        expected=stock0[pid]+oldq-newq
        actual=db.execute('SELECT stock FROM products WHERE id=?',(pid,)).fetchone()[0]
        assert abs(actual-expected)<1e-9
    assert db.execute('SELECT total FROM invoices WHERE id=?',(edit_id,)).fetchone()[0]==subtotal

    # credit cancellation invariant: debt is reversed together with stock
    credit_ino=4
    credit_id,cid,credit_total=db.execute('SELECT id,customer_id,total FROM invoices WHERE invoice_no=?',(credit_ino,)).fetchone()
    debt0=db.execute('SELECT debt FROM parties WHERE id=?',(cid,)).fetchone()[0]
    credit_lines=list(db.execute('SELECT product_id,qty FROM invoice_items WHERE invoice_id=?',(credit_id,)))
    with db:
        for pid,q in credit_lines: db.execute('UPDATE products SET stock=stock+? WHERE id=?',(q,pid))
        db.execute('UPDATE parties SET debt=debt-? WHERE id=?',(credit_total,cid))
        db.execute("UPDATE invoices SET status='CANCELLED',cancelled_at=?,cancel_reason='stress-credit' WHERE id=?",(now.isoformat(),credit_id))
    assert db.execute('SELECT debt FROM parties WHERE id=?',(cid,)).fetchone()[0]==debt0-credit_total
    assert db.execute('SELECT status FROM invoices WHERE id=?',(credit_id,)).fetchone()[0]=='CANCELLED'

    check=db.execute('PRAGMA quick_check').fetchone()[0]
    assert check=='ok'
    assert migration_test()
    counts={
        'products':db.execute('SELECT COUNT(*) FROM products').fetchone()[0],
        'invoices':db.execute('SELECT COUNT(*) FROM invoices').fetchone()[0],
        'invoice_items':db.execute('SELECT COUNT(*) FROM invoice_items').fetchone()[0],
        'expenses':db.execute('SELECT COUNT(*) FROM expenses').fetchone()[0],
        'purchases':db.execute('SELECT COUNT(*) FROM purchases').fetchone()[0],
    }
    result={'database':path,'quick_check':check,'migration_v3_to_v4':'OK','counts':counts,'bulk_insert_ms':{'products':round(product_insert_ms,1),'invoices_and_items':round(invoice_insert_ms,1)},'query_ms':timings,'purchase_invariant':'OK','cancel_invariant':'OK','edit_invariant':'OK','credit_cancel_debt_invariant':'OK'}
    print(json.dumps(result,ensure_ascii=False,indent=2))
    db.close()
    return result

if __name__=='__main__': run()
