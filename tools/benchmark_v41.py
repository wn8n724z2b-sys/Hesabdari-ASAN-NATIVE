#!/usr/bin/env python3
import json, sqlite3, tempfile, time, statistics, os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'HesabdariAsan.Native' / 'PERFORMANCE_BENCHMARK_V41.json'


def timed(cur, sql, params=(), repeats=7):
    values=[]
    rows=None
    for _ in range(repeats):
        t=time.perf_counter()
        rows=cur.execute(sql, params).fetchall()
        values.append((time.perf_counter()-t)*1000)
    return {"median_ms": round(statistics.median(values), 4), "min_ms": round(min(values),4), "max_ms": round(max(values),4), "rows": len(rows or [])}


def main():
    with tempfile.TemporaryDirectory() as td:
        dbp=Path(td)/'scale.sqlite3'
        con=sqlite3.connect(dbp)
        cur=con.cursor()
        cur.executescript('''
PRAGMA journal_mode=OFF;
PRAGMA synchronous=OFF;
PRAGMA temp_store=MEMORY;
PRAGMA cache_size=-131072;
CREATE TABLE digits(n INTEGER PRIMARY KEY);
INSERT INTO digits VALUES(0),(1),(2),(3),(4),(5),(6),(7),(8),(9);
CREATE TABLE products(id INTEGER PRIMARY KEY, name TEXT, barcode TEXT, is_active INTEGER NOT NULL DEFAULT 1);
CREATE TABLE product_barcodes(id INTEGER PRIMARY KEY, product_id INTEGER NOT NULL, barcode TEXT NOT NULL UNIQUE, is_primary INTEGER NOT NULL DEFAULT 0);
CREATE TABLE invoices(id INTEGER PRIMARY KEY, invoice_no INTEGER NOT NULL UNIQUE, customer_name TEXT NOT NULL, payment_type TEXT NOT NULL, total INTEGER NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL);
CREATE TABLE invoice_items(id INTEGER PRIMARY KEY, invoice_id INTEGER NOT NULL, qty REAL NOT NULL);
''')
        # 100,000 products
        cur.execute('''INSERT INTO products(id,name,barcode,is_active)
SELECT x+1, 'Product '||(x+1), printf('BC%08d',x+1), 1 FROM (
 SELECT a.n+10*b.n+100*c.n+1000*d.n+10000*e.n AS x
 FROM digits a,digits b,digits c,digits d,digits e
) WHERE x<100000''')
        cur.execute('INSERT INTO product_barcodes(product_id,barcode,is_primary) SELECT id,barcode,1 FROM products')
        # 1,000,000 invoices
        cur.execute('''INSERT INTO invoices(id,invoice_no,customer_name,payment_type,total,status,created_at)
SELECT x+1,x+1,'Customer '||((x%50000)+1),CASE WHEN x%3=0 THEN 'CREDIT' ELSE 'CASH' END,100+(x%100000),'ACTIVE',printf('2026-01-%02dT%02d:%02d:%02d',1+(x%28),(x/28)%24,(x/672)%60,(x/40320)%60)
FROM (
 SELECT a.n+10*b.n+100*c.n+1000*d.n+10000*e.n+100000*f.n AS x
 FROM digits a,digits b,digits c,digits d,digits e,digits f
) WHERE x<1000000''')
        # 3,000,000 invoice items
        cur.execute('''INSERT INTO invoice_items(invoice_id,qty)
SELECT i.id,1 FROM invoices i CROSS JOIN (SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3)''')
        con.commit()
        cur.executescript('''
CREATE INDEX idx_products_barcode ON products(barcode);
CREATE INDEX idx_product_barcodes_barcode ON product_barcodes(barcode);
CREATE INDEX idx_product_barcodes_product ON product_barcodes(product_id);
CREATE INDEX idx_invoices_status_created ON invoices(status,created_at DESC);
CREATE INDEX idx_invoices_created_id ON invoices(created_at DESC,id DESC);
CREATE INDEX idx_invoice_items_invoice_id ON invoice_items(invoice_id,id);
ANALYZE;
''')
        con.commit()
        target='BC09999999' if False else 'BC00099999'
        old_sql='''SELECT p.id FROM products p WHERE p.is_active=1 AND (p.barcode=? OR EXISTS(SELECT 1 FROM product_barcodes pb WHERE pb.product_id=p.id AND pb.barcode=?)) LIMIT 1'''
        new_sql='''SELECT p.id FROM product_barcodes hit JOIN products p ON p.id=hit.product_id WHERE p.is_active=1 AND hit.barcode=? LIMIT 1'''
        result={
          "scale": {"products":100000,"invoices":1000000,"invoice_items":3000000},
          "sqlite_version": sqlite3.sqlite_version,
          "quick_check": cur.execute('PRAGMA quick_check').fetchone()[0],
          "barcode_lookup_old": timed(cur,old_sql,(target,target)),
          "barcode_lookup_indexed": timed(cur,new_sql,(target,)),
          "archive_first_page": timed(cur,"SELECT id,invoice_no,total,created_at FROM invoices ORDER BY created_at DESC,id DESC LIMIT 50 OFFSET 0"),
          "archive_deep_page_900k": timed(cur,"SELECT id,invoice_no,total,created_at FROM invoices ORDER BY created_at DESC,id DESC LIMIT 50 OFFSET 900000"),
          "database_bytes": dbp.stat().st_size,
        }
        OUT.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(result,ensure_ascii=False,indent=2))

if __name__=='__main__': main()
