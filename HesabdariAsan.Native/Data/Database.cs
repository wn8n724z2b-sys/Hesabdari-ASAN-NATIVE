using Microsoft.Data.Sqlite;

namespace HesabdariAsan.Native.Data;

public static class Database
{
    public const int CurrentSchemaVersion = 4;

    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HesabdariAsan");

    public static string DbPath => Path.Combine(AppDataDir, "hesabdari_asan_v4.sqlite3");
    public static string BackupsDir => Path.Combine(AppDataDir, "Backups");

    public static SqliteConnection Open()
    {
        Directory.CreateDirectory(AppDataDir);
        var connection = new SqliteConnection($"Data Source={DbPath};Cache=Shared");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public static void Initialize()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(BackupsDir);

        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS app_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS work_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    started_at TEXT NOT NULL,
    ended_at TEXT NULL,
    duration_seconds INTEGER NOT NULL DEFAULT 0,
    last_pulse_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    barcode TEXT NULL,
    category TEXT NOT NULL DEFAULT 'عمومی',
    unit TEXT NOT NULL DEFAULT 'عدد',
    purchase_price INTEGER NOT NULL DEFAULT 0,
    sale_price INTEGER NOT NULL DEFAULT 0,
    stock REAL NOT NULL DEFAULT 0,
    min_stock REAL NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_products_name ON products(name);
CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(barcode);
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category);

CREATE TABLE IF NOT EXISTS parties (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    type TEXT NOT NULL CHECK(type IN ('CUSTOMER','COMPANY')),
    name TEXT NOT NULL,
    phone TEXT NULL,
    address TEXT NULL,
    debt INTEGER NOT NULL DEFAULT 0,
    is_pinned INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_parties_name ON parties(name);
CREATE INDEX IF NOT EXISTS idx_parties_phone ON parties(phone);
CREATE INDEX IF NOT EXISTS idx_parties_type ON parties(type);

CREATE TABLE IF NOT EXISTS expenses (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    category TEXT NOT NULL DEFAULT 'هزینه',
    amount INTEGER NOT NULL,
    note TEXT NULL,
    created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_expenses_created_at ON expenses(created_at);

CREATE TABLE IF NOT EXISTS customer_receipts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    party_id INTEGER NOT NULL,
    amount INTEGER NOT NULL,
    note TEXT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY(party_id) REFERENCES parties(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_customer_receipts_created_at ON customer_receipts(created_at);

CREATE TABLE IF NOT EXISTS supplier_payments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    party_id INTEGER NOT NULL,
    amount INTEGER NOT NULL,
    note TEXT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY(party_id) REFERENCES parties(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_supplier_payments_created_at ON supplier_payments(created_at);

CREATE TABLE IF NOT EXISTS invoices (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    invoice_no INTEGER NOT NULL UNIQUE,
    customer_id INTEGER NULL,
    customer_name TEXT NOT NULL DEFAULT 'مشتری عمومی',
    payment_type TEXT NOT NULL CHECK(payment_type IN ('CASH','CREDIT')),
    subtotal INTEGER NOT NULL DEFAULT 0,
    discount INTEGER NOT NULL DEFAULT 0,
    total INTEGER NOT NULL DEFAULT 0,
    paid INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'ACTIVE',
    cancelled_at TEXT NULL,
    cancel_reason TEXT NULL,
    updated_at TEXT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY(customer_id) REFERENCES parties(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_invoices_created_at ON invoices(created_at);
CREATE INDEX IF NOT EXISTS idx_invoices_customer_id ON invoices(customer_id);

CREATE TABLE IF NOT EXISTS invoice_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    invoice_id INTEGER NOT NULL,
    product_id INTEGER NULL,
    product_name TEXT NOT NULL,
    barcode TEXT NULL,
    qty REAL NOT NULL,
    unit_price INTEGER NOT NULL,
    purchase_price INTEGER NOT NULL DEFAULT 0,
    line_total INTEGER NOT NULL,
    FOREIGN KEY(invoice_id) REFERENCES invoices(id) ON DELETE CASCADE,
    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_invoice_items_invoice ON invoice_items(invoice_id);

CREATE TABLE IF NOT EXISTS inventory_ledger (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    movement_type TEXT NOT NULL,
    qty REAL NOT NULL,
    balance_after REAL NOT NULL,
    reference_type TEXT NULL,
    reference_id INTEGER NULL,
    note TEXT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_inventory_product ON inventory_ledger(product_id);
CREATE INDEX IF NOT EXISTS idx_inventory_created_at ON inventory_ledger(created_at);

CREATE TABLE IF NOT EXISTS purchases (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NULL,
    product_name TEXT NOT NULL,
    quantity REAL NOT NULL DEFAULT 0,
    units REAL NOT NULL DEFAULT 0,
    unit_cost INTEGER NOT NULL DEFAULT 0,
    total INTEGER NOT NULL DEFAULT 0,
    payment_type TEXT NOT NULL DEFAULT 'CASH',
    supplier_id INTEGER NULL,
    note TEXT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE SET NULL,
    FOREIGN KEY(supplier_id) REFERENCES parties(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_purchases_created_at ON purchases(created_at);
CREATE INDEX IF NOT EXISTS idx_purchases_product ON purchases(product_id);
CREATE INDEX IF NOT EXISTS idx_purchases_supplier ON purchases(supplier_id);

CREATE TABLE IF NOT EXISTS financial_periods (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    period_no INTEGER NOT NULL UNIQUE,
    started_at TEXT NOT NULL,
    ended_at TEXT NULL,
    summary_json TEXT NULL
);

CREATE TABLE IF NOT EXISTS audit_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    created_at TEXT NOT NULL,
    action TEXT NOT NULL,
    entity_type TEXT NULL,
    entity_id TEXT NULL,
    payload_json TEXT NULL,
    actor TEXT NOT NULL DEFAULT 'Admin',
    invoice_no INTEGER NULL
);
CREATE INDEX IF NOT EXISTS idx_audit_created_at ON audit_log(created_at);
CREATE INDEX IF NOT EXISTS idx_audit_invoice_no ON audit_log(invoice_no);
CREATE INDEX IF NOT EXISTS idx_audit_entity ON audit_log(entity_type,entity_id);

CREATE TABLE IF NOT EXISTS invoice_revisions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    invoice_id INTEGER NULL,
    invoice_no INTEGER NOT NULL,
    revision_no INTEGER NOT NULL,
    changed_at TEXT NOT NULL,
    action TEXT NOT NULL,
    actor TEXT NOT NULL DEFAULT 'Admin',
    snapshot_json TEXT NOT NULL,
    FOREIGN KEY(invoice_id) REFERENCES invoices(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_invoice_revisions_invoice ON invoice_revisions(invoice_no, changed_at);

CREATE TABLE IF NOT EXISTS legacy_id_map (
    entity_type TEXT NOT NULL,
    legacy_id TEXT NOT NULL,
    native_id INTEGER NOT NULL,
    PRIMARY KEY(entity_type, legacy_id)
);

INSERT OR IGNORE INTO parties(type,name,phone,address,debt,is_pinned,created_at)
SELECT 'CUSTOMER','مشتری عمومی','','',0,1,datetime('now')
WHERE NOT EXISTS(SELECT 1 FROM parties WHERE type='CUSTOMER' AND name='مشتری عمومی');
";
        cmd.ExecuteNonQuery();

        EnsureColumn(db, "invoices", "status", "TEXT NOT NULL DEFAULT 'ACTIVE'");
        EnsureColumn(db, "invoices", "cancelled_at", "TEXT NULL");
        EnsureColumn(db, "invoices", "cancel_reason", "TEXT NULL");
        EnsureColumn(db, "invoices", "updated_at", "TEXT NULL");
        EnsureColumn(db, "purchases", "note", "TEXT NULL");
        using (var indexes = db.CreateCommand())
        {
            indexes.CommandText = "CREATE INDEX IF NOT EXISTS idx_invoices_status ON invoices(status);";
            indexes.ExecuteNonQuery();
        }

        using var version = db.CreateCommand();
        version.CommandText = @"INSERT INTO app_meta(key,value) VALUES('schema_version',$v)
ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        version.Parameters.AddWithValue("$v", CurrentSchemaVersion.ToString());
        version.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection db, string table, string column, string definition)
    {
        using var check = db.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        }
        reader.Close();
        using var alter = db.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    public static string QuickCheck()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA quick_check;";
        return Convert.ToString(cmd.ExecuteScalar()) ?? "unknown";
    }
}
