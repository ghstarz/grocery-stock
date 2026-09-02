PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Category (
    CategoryId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL COLLATE NOCASE UNIQUE
);

CREATE TABLE IF NOT EXISTS Supplier (
    SupplierId INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    Phone TEXT NULL,
    Email TEXT NULL
);

CREATE TABLE IF NOT EXISTS StockItem (
    ItemId INTEGER PRIMARY KEY AUTOINCREMENT,
    ItemCode TEXT NOT NULL COLLATE NOCASE UNIQUE,
    Name TEXT NOT NULL,
    CategoryId INTEGER NOT NULL,
    Unit TEXT NOT NULL,
    CostPriceCents INTEGER NOT NULL CHECK (CostPriceCents >= 0),
    SalePriceCents INTEGER NOT NULL CHECK (SalePriceCents >= 0),
    ReorderLevel INTEGER NOT NULL CHECK (ReorderLevel >= 0),
    IsPerishable INTEGER NOT NULL CHECK (IsPerishable IN (0, 1)),
    IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
    FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId)
);

CREATE TABLE IF NOT EXISTS Batch (
    BatchId INTEGER PRIMARY KEY AUTOINCREMENT,
    ItemId INTEGER NOT NULL,
    BatchCode TEXT NOT NULL,
    ExpiryDate TEXT NOT NULL,
    UNIQUE (ItemId, BatchCode),
    UNIQUE (BatchId, ItemId),
    FOREIGN KEY (ItemId) REFERENCES StockItem(ItemId)
);

CREATE TABLE IF NOT EXISTS StockMovement (
    MovementId INTEGER PRIMARY KEY AUTOINCREMENT,
    ItemId INTEGER NOT NULL,
    BatchId INTEGER NULL,
    MovementType TEXT NOT NULL CHECK (MovementType IN ('In', 'Out')),
    Reason TEXT NOT NULL,
    Quantity INTEGER NOT NULL CHECK (Quantity > 0),
    UnitCostCents INTEGER NOT NULL CHECK (UnitCostCents >= 0),
    MovementDate TEXT NOT NULL,
    SupplierId INTEGER NULL,
    RecordedBy TEXT NOT NULL,
    FOREIGN KEY (ItemId) REFERENCES StockItem(ItemId),
    FOREIGN KEY (BatchId, ItemId) REFERENCES Batch(BatchId, ItemId),
    FOREIGN KEY (SupplierId) REFERENCES Supplier(SupplierId)
);

CREATE INDEX IF NOT EXISTS IX_StockMovement_ItemId ON StockMovement(ItemId);
CREATE INDEX IF NOT EXISTS IX_StockMovement_BatchId ON StockMovement(BatchId);
