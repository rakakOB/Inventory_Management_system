# Build / Test / Debug Context — Inventory Management v2

> **Handoff document.** Everything a builder (deepseek or the developer) needs to
> build, run, test, and debug the v2 transformation **without the original
> conversation**. The code is complete and must compile with **zero errors**.
> Read the actual source for exact member names — this document states the
> contract and the invariants, it does not replace the code.

---

## 1. Project facts

| Fact | Value |
|---|---|
| Project root | `C:\e\Projects\Inventory_Management\Inventory_MS\Inventory_MS\` |
| Type | ASP.NET Core Razor Pages web app |
| Target framework | **net10.0** (do NOT change — see §7, flag #1) |
| Nullable / ImplicitUsings | enabled |
| UserSecretsId | `1f275792-63ad-4e63-9369-7a7cb9027c39` |
| NuGet packages | `Google.Apis.Sheets.v4` 1.75.0.4178 (only package) |
| Database | **None** — a Google Spreadsheet is the live database (Sheets API v4, service-account auth) |
| Namespaces | `InventoryManagement.Models`, `InventoryManagement.Services`, `InventoryManagement.Pages.*` |
| Build entry | `dotnet build` at project root (user builds, tests, fixes errors) |

---

## 2. What changed in the v2 transformation (spec at a glance)

1. **New spreadsheet schema — 8 tabs** (exact layout in §3). Old tabs
   (`PCB_Inventory`, `Panel_Inventory`, `Damages_Components`) are gone.
2. **No `Sl.No.` column anywhere** — `UniqueCode` is a real identifier, never
   renumbered. Rows are append-only; the app sorts **in memory** when displaying.
3. **Costs are tax-inclusive**: `TotalCost = TotalQuantity × CostPerUnit`.
   No GST anywhere.
4. **Models**: deleted `PcbComponent`, `PanelComponent`, `DamageRecord`,
   `InventoryItemBase`, `Tool`. Created `MasterItem`, `Supplier`, `InventoryItem`
   (**one generic model for all 4 category sheets**), `UsedItem`, `DamagedItem`,
   plus a static cell-parsing helper `SheetCell`.
5. **GoogleSheetsService**: public API and authentication **unchanged**.
   Removed the private `RenumberSlNoAsync` and its call in `DeleteRowAsync`.
   Updated 2 stale comments. No sorting in the service.
6. **All pages rewritten** (full list in §4.3). `Program.cs` and
   `appsettings.json` **unchanged**.
7. **Navbar has 11 links**: Home, Electronics, Electrical, Tools & Instruments,
   Modules, Report Usage, Usage History, Report Damage, Damage History,
   Suppliers, Master List.

---

## 3. The spreadsheet schema (the live database)

**Row 1 is the header in every tab.** All row indexes are **1-based and include
the header** (data row list-position `i` ⇒ sheet row `i + 1`). Dates are
`yyyy-MM-dd` strings; money is decimal.

### Tab 1 — `Master` (7 cols)
`UniqueCode | ComponentName | Category | Brand | Description | Unit | MinStockAlert`

### Tab 2 — `Suppliers` (2 cols)
`SupplierName | ContactInfo`

### Tabs 3–6 — `Electronics_Inventory`, `Electrical_Inventory`, `Tools_Inventory`, `Modules_Inventory` (11 cols, identical)
`UniqueCode | ComponentName | Brand | TotalQuantity | Remaining | InvoiceNo | CostPerUnit | TotalCost | Supplier | DateOfPurchase | Remarks`

### Tab 7 — `Used_Components` (7 cols)
`UniqueCode | ComponentName | Category | BatchPurchaseDate | UsedDate | QuantityUsed | Remarks`

### Tab 8 — `Damaged_Components` (9 cols)
`UniqueCode | ComponentName | Category | BatchPurchaseDate | DamageDate | QuantityDamaged | InvoiceNo | CostPerUnit | Remarks`

> **The spreadsheet must have these exact tab names.** If a tab is missing or
> named differently (old names: `PCB_Inventory`, `Panel_Inventory`,
> `Damages_Components`), the service throws
> `Tab '<name>' was not found in the spreadsheet.`

**Category ↔ sheet/prefix mapping (single source of truth, static methods on
`InventoryItem`):**

| Category | Sheet | UniqueCode prefix |
|---|---|---|
| Electronics | Electronics_Inventory | `E-` |
| Electrical | Electrical_Inventory | `EL-` |
| Tools | Tools_Inventory | `T-` |
| Modules | Modules_Inventory | `M-` |

---

## 4. Code map

### 4.1 Models (`Inventory_MS\Models\`) — all namespace `InventoryManagement.Models`

| File | Purpose |
|---|---|
| `SheetCell.cs` | Static helpers: `Cell(row, i)`, `SafeInt(row, i)` (decimal.TryParse → (int)v), `SafeDecimal(row, i)` — all `NumberStyles.Any` + `CultureInfo.InvariantCulture`. Every model uses these in `FromRow`. |
| `MasterItem.cs` | `SheetName="Master"`, `ColumnCount=7`. Props: RowIndex, UniqueCode, ComponentName **[Required]**, Category **[Required]**, Brand, Description, Unit, MinStockAlert (Range 0–999999, **default 5**). `FromRow`/`ToRow` order: `[UniqueCode, ComponentName, Category, Brand, Description, Unit, MinStockAlert]`. |
| `Supplier.cs` | `SheetName="Suppliers"`, `ColumnCount=2`. SupplierName **[Required]**, ContactInfo. `ToRow = [SupplierName, ContactInfo]`. |
| `InventoryItem.cs` | `SheetNameFor(category)` / `CodePrefixFor(category)` static switches (table above). 11 props in the schema order; `RecalculateCosts()` → `TotalCost = Math.Round(TotalQuantity * CostPerUnit, 2)`. Validation: TotalQuantity Range(1,999999), Remaining Range(0,999999), CostPerUnit Range(0,99999999). |
| `UsedItem.cs` | `SheetName="Used_Components"`, 7 cols. QuantityUsed Range(1,999999). |
| `DamagedItem.cs` | `SheetName="Damaged_Components"`, 9 cols. QuantityDamaged Range(1,999999). |

### 4.2 `Services\GoogleSheetsService.cs` (namespace `InventoryManagement.Services`)

Singleton; the **only** class that touches the Sheets API. Public API (unchanged):

| Method | Behavior |
|---|---|
| `GetRowsAsync(sheetName)` | `Values.Get(spreadsheetId, "{sheet}!A1:Z1000")`; returns `IList<IList<object>>` **including the header row**; `null` → empty list. |
| `AppendRowAsync(sheetName, row)` | `Values.Append`, `USERENTERED` + `INSERTROWS`; returns the **1-based row index** of the new row (parsed from `Updates.UpdatedRange`, fallback: data-row count + 2). |
| `UpdateRowAsync(sheetName, rowIndex, row)` | Overwrites `A{rowIndex}:{ColumnLetter(count)}{rowIndex}` with `USERENTERED`. |
| `DeleteRowAsync(sheetName, rowIndex)` | BatchUpdate `DeleteDimension` ROWS, 0-based `StartIndex = rowIndex - 1`. **Deliberately does NOT renumber** — UniqueCode is an identifier. |
| `GetCellValueAsync(sheetName, rowIndex, columnIndex)` | Single cell as text. |

Internals that matter while debugging:
- `SemaphoreSlim _writeLock` serializes **all writes** (append/update/delete). Multi-step page logic (usage/damage reports) is therefore safe against interleaving.
- `ConcurrentDictionary<string,int> _sheetIdCache` — tab ids fetched once per sheet name (needed for row deletion).
- `LoadCredential(credentialsPath)`: if the path is non-empty **and the file exists** → `GoogleCredential.FromFile(path).CreateScoped(SheetsService.Scope.Spreadsheets)`; otherwise → Application Default Credentials. Keep this logic untouched.
- Constructor: `new GoogleSheetsService(spreadsheetId, credentialsPath ?? "")`.

### 4.3 Pages (33 files under `Inventory_MS\Pages\`) — namespace `InventoryManagement.Pages.*`

```
Index.cshtml(.cs)                         dashboard
Master\{Index, Create, Edit, Delete}.cshtml(.cs)
Suppliers\{Index, Create, Edit, Delete}.cshtml(.cs)
ElectronicsInventory\{Index, AddStock, Edit, Delete}.cshtml(.cs)
ElectricalInventory\{Index, AddStock, Edit, Delete}.cshtml(.cs)
ToolsInventory\{Index, AddStock, Edit, Delete}.cshtml(.cs)
ModulesInventory\{Index, AddStock, Edit, Delete}.cshtml(.cs)
Usage\{Report, History, HistoryEdit, HistoryDelete}.cshtml(.cs)
Damage\{Report, History, HistoryEdit, HistoryDelete}.cshtml(.cs)
Shared\_Layout.cshtml (11 navbar links) · Error.cshtml(.cs) · _ViewImports · _ViewStart
```

The four category folders are **identical except** `private const string SheetName`
and the category constant. The Usage and Damage folders are identical except
UsedDate/QuantityUsed vs DamageDate/QuantityDamaged (and Damage's extra
InvoiceNo/CostPerUnit fields).

---

## 5. Page-by-page behavior spec (the contract to test against)

### 5.1 Dashboard (`/Index`)
- Loads Master + all 4 category sheets **in parallel** (`Task.WhenAll`), each in
  try/catch that degrades to an empty list — the dashboard must never crash
  because one sheet failed.
- `MasterCount` = Master rows.
- `LowStockCount`: build `Dictionary<UniqueCode, MinStockAlert>` from Master
  (OrdinalIgnoreCase); then count every inventory row across the 4 category
  sheets where `Remaining < MinStockAlert`. Rows with no Master entry are skipped.
- Renders stat cards + quick links (each category, Report Usage, Report Damage).

### 5.2 Master CRUD
- **Create**: category dropdown (Electronics/Electrical/Tools/Modules, required).
  UniqueCode generation: prefix from category, scan all Master rows,
  `code.StartsWith(prefix, OrdinalIgnoreCase) && int.TryParse(code[prefix.Length..], out n)`
  → max suffix; new code = `$"{prefix}{max + 1:D3}"` → **`E-001`, `EL-014`, …**
  (zero-padded to 3 digits). If the prefix can't be produced, a ModelState error
  is added.
- **Edit**: all fields editable **except UniqueCode (read-only)** — it is the
  join key to every other sheet.
- **Delete**: confirmation page → `DeleteRowAsync`. No renumbering anywhere.

### 5.3 Suppliers CRUD
- Simple CRUD, sorted by SupplierName.
- ⚠ **Edit page binds `RowIndex` explicitly** (`[BindProperty] public int RowIndex`,
  set in `OnGet` from `supplier.RowIndex`; the view posts
  `asp-route-id="@Model.RowIndex"`). Do not regress this — it was a bug fix
  (v1 posted a nonexistent ViewData key).

### 5.4 Category inventory pages (×4)
- **Index**: parse rows → `List<InventoryItem>`, `OrderBy(ComponentName, OrdinalIgnoreCase)`.
- **AddStock** (the only creation path — stock is always attached to a Master item):
  - Component dropdown: Master items filtered to this category, labeled
    `UniqueCode – ComponentName` (required).
  - Fields: Quantity [Range(1,999999)] (required), InvoiceNo, **CostPerUnit
    [Range(0.01, 99999999)] (required)**, Supplier (dropdown from Suppliers
    sheet), DateOfPurchase (defaults to today), Remarks.
  - **On POST**: find Master item by UniqueCode → scan the category sheet for an
    existing row with the same UniqueCode:
    - **exists** → `TotalQuantity += qty`, `Remaining += qty`, `CostPerUnit` and
      `Supplier` **overwritten** with the new values, `RecalculateCosts()`,
      `UpdateRowAsync`.
    - **missing** → create `InventoryItem` seeded from Master (ComponentName,
      Brand), `TotalQuantity = Remaining = qty`, `AppendRowAsync`.
  - Redirect to Index with `TempData["Success"]`.
  - **Consequence (invariant): at most ONE row per UniqueCode per category sheet.**
- **Edit**: UniqueCode / ComponentName / Brand are readonly inputs that
  round-trip (display-only in the view). TotalQuantity, Remaining, InvoiceNo,
  CostPerUnit, Supplier, DateOfPurchase, Remarks are editable. On save:
  `RecalculateCosts()` (TotalCost = TotalQty × Cost/Unit, rounded 2dp) then
  `UpdateRowAsync`. **No automatic TotalQuantity↔Remaining reconciliation** —
  the user manages it; sheet convention is `Remaining ≤ TotalQuantity`.
  Supplier dropdown: if the current supplier is missing from the Suppliers
  sheet, it is **prepended** to the options so the row can still be saved.
- **Delete**: confirmation → `DeleteRowAsync`.

### 5.5 Usage Report (`/Usage/Report`)
- `OnGet`: load all 4 category sheets in parallel (try/catch per category),
  keep only `Remaining > 0` items, group into `ComponentsByCategory`. The view
  renders **one select per category** labeled
  `UniqueCode – ComponentName (Remaining: N)`; JS toggles the visible select
  and shows a stock hint (client-side convenience only — the real check is
  server-side).
- Fields: QuantityUsed [Range(1,999999)], UsedDate (default today), Remarks.
- **On POST, in this exact order:**
  1. Validate category via `InventoryItem.SheetNameFor(category)` → invalid = Fail.
  2. Re-load that category's rows fresh (validation uses current stock).
  3. Find item by UniqueCode → missing = Fail.
  4. `QuantityUsed > item.Remaining` → **Fail** with "only N in stock" message.
  5. Snapshot `originalRow = item.ToRow()`.
  6. `Remaining -= QuantityUsed`; `UpdateRowAsync` (category sheet).
  7. Build `UsedItem` with **`BatchPurchaseDate = item.DateOfPurchase`** (the
     inventory row's date); `AppendRowAsync` (Used_Components).
  8. **If the append throws → rollback**: `UpdateRowAsync` the original row back,
     then rethrow. The two sheets must never drift apart.
  9. Success → `TempData["Success"]` → redirect `/Usage/History`.

### 5.6 Usage History (`/Usage/History` + Edit/Delete)
- **History**: all rows from Used_Components; sort `ComponentName` ascending,
  then `UsedDate` **descending**. Columns: UniqueCode, ComponentName, Category,
  BatchPurchaseDate, UsedDate, QuantityUsed, Remarks + actions.
- **HistoryEdit** (`?id=<rowIndex>`): loads the **original record first**,
  then the inventory row via the record's Category + UniqueCode.
  - If the record's quantity changed:
    `item.Remaining += original.QuantityUsed` (reverse old);
    validate `Record.QuantityUsed > item.Remaining` → Fail with
    "after reversing the previous usage" message;
    `item.Remaining -= Record.QuantityUsed` (deduct new);
    `UpdateRowAsync` inventory row, then `UpdateRowAsync` the record.
  - If the inventory row no longer exists → skip the stock adjustment, still
    save the record.
- **HistoryDelete**: confirmation → on confirm:
  `item.Remaining += Record.QuantityUsed` (if the row exists), then
  `DeleteRowAsync` the record.

### 5.7 Damage Report / History (mirror of 5.5/5.6)
- Same mechanics with DamageDate / QuantityDamaged, writing to
  `Damaged_Components`.
- Extra optional fields: **InvoiceNo falls back to the inventory row's
  InvoiceNo if left blank; CostPerUnit falls back to the inventory row's
  CostPerUnit if left at 0.**
- HistoryEdit allows editing InvoiceNo/CostPerUnit too.

---

## 6. Invariants & conventions (preserve these while debugging)

1. Row indexes are **1-based and include the header**; parse list-position `i`
   as sheet row `i + 1`.
2. **One row per UniqueCode per category sheet** (enforced by Add Stock's
   update-or-append). All later lookups (usage, damage, history rollback)
   rely on locating a row by UniqueCode alone.
3. **UniqueCode is immutable** once created; never renumber anything.
4. All writes go through the service's 5 methods (the service owns the lock).
   Never write raw Sheets requests from a page.
5. Money: `decimal`, `InvariantCulture` everywhere (display `N2`).
6. Dates: `yyyy-MM-dd` strings; date inputs default to today.
7. Error/UX patterns: `TempData["Success"]` on redirect, `ModelState` +
   `Fail(message)` to re-render on error, `id is not > 0` → NotFound,
   `FindAsync` lookups on `RowIndex`.

---

## 7. Known flags / watch-outs for the builder

1. **TargetFramework is `net10.0`** (the original spec said ".NET 8"; the
   project was deliberately kept as-is at net10.0 — the code was written
   against the existing csproj). If your machine only has an older SDK, the
   *only* acceptable change is `TargetFramework` in the csproj — but do that
   consciously, since it was flagged to the user deliberately. Both
   `bin/Debug/net8.0` and `bin/Debug/net10.0` artifacts exist in the tree —
   ignore them; a fresh `dotnet build` regenerates.
2. **The spreadsheet must use the new tab names** (§3) or every page throws
   "Tab 'X' was not found". This is a data change in Google Sheets, not code.
3. **UniqueCode pads to 3 digits** (`E-001`). A 4th digit appears naturally at
   the 1000th item (`E-1000`) — safe because generation scans for the max
   numeric suffix.
4. **Suppliers/Edit** — the `RowIndex` bind fix (§5.3) is a deliberate
   deviation; keep it.
5. **`appsettings.json` quirk (pre-existing, harmless):** it contains key
   `GoogleApplicationCredentials`, but `Program.cs` looks up
   `GOOGLE_APPLICATION_CREDENTIALS` (with underscores) — JSON config keys are
   case-insensitive but **not** underscore-insensitive, so that JSON key is
   effectively dead. The environment variable is the real mechanism; leave
   both as-is.
6. **`claude_works/README.md` is an old copy** (PCB-era) — not part of the
   build. The project's own `Inventory_MS/README.md` was updated to the new
   schema. Ignore `.vs/` (VS cache) and `bin/`/`obj/`.
7. **`app.yaml` is old and being reworked** — deployment is handled separately
   (see `claude_works/readme_v2.md` §7 for the code's deployment contract).
   Not needed for build/test.

---

## 8. Build, run & test checklist

### Build
```powershell
cd C:\e\Projects\Inventory_Management\Inventory_MS\Inventory_MS
dotnet build
```
Expected: **0 errors, 0 warnings-as-errors**. If errors appear, they are in the
transformation — fix by reading the page model + its view side by side
(BindProperty ↔ asp-for/asp-route must match; model namespace imports in the
view must match `_ViewImports`).

### Run locally
```powershell
# 1) Tell the app which spreadsheet (must exist with the 8 tabs from §3)
dotnet user-secrets set SpreadsheetId "<spreadsheet-id from the sheet URL>"

# 2) Tell it who it is (service account key file)
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\path\to\service-account-key.json"

# 3) The spreadsheet must be shared with the service-account email as Editor
dotnet run
```

### Click-through test plan (in order)
1. **Dashboard** — loads without errors; counts match the sheet.
2. **Master** — Create an item in each category; verify codes `E-001`,
   `EL-001`, `T-001`, `M-001`; verify the next created code is `…002`.
   Edit it; confirm UniqueCode field is readonly. Delete it (confirm page).
3. **Suppliers** — Create/edit/delete; edit round-trips the right row.
4. **Add Stock** — (a) new UniqueCode: row appears with
   `TotalQuantity = Remaining = qty`; (b) same UniqueCode again: quantities
   **accumulate** and CostPerUnit/Supplier get overwritten;
   (c) verify in Google Sheets that `TotalCost = TotalQuantity × CostPerUnit`.
5. **Report Usage** — happy path: Remaining decreases, record appears in
   Usage History with correct BatchPurchaseDate (= the inventory row's
   DateOfPurchase). Try qty > Remaining: must be rejected with the stock
   message. (Optionally: kill the spreadsheet write access mid-flow and check
   the rollback restores Remaining.)
6. **Usage History Edit** — change qty 3→1: stock goes up by 3, down by 1
   (net +2 over the edit). Set a too-large qty: rejected, stock unchanged.
7. **Usage History Delete** — stock restored by the record's qty; record gone.
8. **Damage Report / History** — same as 5–7; leave InvoiceNo/CostPerUnit
   blank and verify they inherit the inventory row's values.
9. **Cross-check** — after every step, open the spreadsheet and confirm the
   physical rows match (row indexes shift only on delete; nothing is
   renumbered).

### Sanity rules while debugging
- A compile error in a `.cshtml` often surfaces as a Razor error pointing at a
  `@model`/`asp-for` mismatch — the fix is in the page model or view, not the
  service.
- If "Tab not found" appears at runtime → spreadsheet tabs (data), not code.
- If writes 403 → spreadsheet not shared with the service account (data/ACL),
  not code.

---

## 9. Do NOT touch (hard boundaries)

- `Program.cs` and `appsettings*.json` — configuration, auth, middleware chain.
- `GoogleSheetsService` public API and credential resolution — the lock,
  `A1:Z1000`, row-index conventions, delete-without-renumbering.
- Sheet schema and column order — `FromRow`/`ToRow` map 1:1 to §3; changing
  either side without the other corrupts data.
- The "no sorting in the service" rule; the "one row per UniqueCode" invariant.
