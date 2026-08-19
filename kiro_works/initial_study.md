# IMS / GIMS — Initial Study

**Author:** Kiro
**Date:** 2026-08-18
**Subject:** Inventory Management System (IMS / GIMS), currently at version 2.0
**Purpose:** Baseline understanding of the project before v2.2 development begins.

---

## 0. Scope of this study — what was actually covered

### Folders examined

| Folder | Coverage |
|---|---|
| `Inventory_MS\Inventory_MS\` | **Complete.** Every hand-written source file read (all `.cs`, all `.cshtml`, all config). Third-party `wwwroot\lib` assets were listed but not read. |
| `claude_works\` | **Complete.** All 8 text documents read (delegated to a read-only sub-agent). `docs\SRD.docx` skipped — `SRD.md` is its source. |
| `claude_interaction\...\6022a5a2-b81c-460d-954d-cb8588bae497.jsonl` | **Complete** (1.9 MB transcript, read in chunks by a read-only sub-agent). |

Explicitly **out of scope** by your instruction: `Inventory_MS_git\`, all git history/logs, the other `claude_interaction` transcripts, the two `.zip` archives, and the service-account `.json` key.

### Files read in the project

- **Config / project:** `Inventory_MS.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `app.yaml`, `Properties\launchSettings.json`, `.gitignore`, `README.md`
- **Models (6):** `MasterItem.cs`, `Supplier.cs`, `InventoryItem.cs`, `UsedItem.cs`, `DamagedItem.cs`, `SheetCell.cs`
- **Services (1):** `GoogleSheetsService.cs`
- **Pages (all 33 + shared):** Dashboard, Master ×4, Suppliers ×4, four category folders ×4 each, Usage ×4, Damage ×4, `_Layout.cshtml`, `_ViewImports.cshtml`, `_ViewStart.cshtml`, `_ValidationScriptsPartial.cshtml`, `Error`
- **Static:** `wwwroot\css\site.css`, `Pages\Shared\_Layout.cshtml.css`

### Verification actually performed

1. **Structural diff** of the four category page folders against each other, to test whether they are copy-paste clones. They are — see §7.1.
2. **Build** — `dotnet build` on .NET SDK 10.0.400: **succeeded, 0 errors, 2 warnings**. Both warnings are the two already-known deprecations (§9.1). Note: a .NET 10 SDK *is* present on this machine, so builds can be run here; earlier Claude sessions could not, which is why build verification was previously handed to you.

### Not verified

- No page was executed against the live spreadsheet. All runtime behaviour below is read from code, not observed.
- Google Sheets API quota behaviour and Cloud Run deployment state are unverified.

---

## 1. What the system is

An internal web application for tracking electronics/electrical components, tools and modules. It records what is in stock, what was consumed, and what was damaged. There is **no sales, invoicing-out, or customer side** — it is purely internal stock control.

Its distinguishing architectural choice: **there is no database**. A single Google Spreadsheet with 8 tabs *is* the database, accessed live over the Sheets API v4 on every page load. This was deliberate — it costs nothing, needs no DB administration, and lets staff read the raw data in Google Sheets directly.

Branding is inconsistent: the code and navbar say "Smart Signage Inventory" (a leftover from v1, when this was built for a smart-signage-board project), while the documentation calls it "GIMS — General Inventory Management System". Worth settling in v2.2.

---

## 2. Tech stack

| Layer | Choice |
|---|---|
| Framework | ASP.NET Core **Razor Pages**, `net10.0` |
| Data store | Google Sheets API v4, `Google.Apis.Sheets.v4` **1.75.0.4178** (the only NuGet package) |
| Auth to Google | Service account key file, falling back to Application Default Credentials |
| UI | Bootstrap 5.3.3 from CDN, plus a local copy in `wwwroot\lib` used only by the jQuery validation partial |
| App auth | **None** |
| Hosting | Google Cloud Run, service `inventory-ms`, region `us-central1`, `--allow-unauthenticated` |
| Local dev | `dotnet user-secrets` for `SpreadsheetId`, env var for credentials |

---

## 3. Solution layout

```
Inventory_MS\
├─ Inventory_MS.slnx
└─ Inventory_MS\                      ← the actual project
   ├─ Inventory_MS.csproj             net10.0, nullable + implicit usings on
   ├─ Program.cs                      startup, DI, Cloud Run port binding
   ├─ appsettings.json / .Development.json
   ├─ app.yaml                        stale App Engine descriptor (see §9.3)
   ├─ README.md                       v1-era, describes App Engine deploy
   ├─ Models\                         6 files, all row<->object mapping
   ├─ Services\GoogleSheetsService.cs the entire data layer
   ├─ Pages\
   │  ├─ Index                        dashboard
   │  ├─ Master\                      Index, Create, Edit, Delete
   │  ├─ Suppliers\                   Index, Create, Edit, Delete
   │  ├─ ElectronicsInventory\        Index, AddStock, Edit, Delete
   │  ├─ ElectricalInventory\         ┐
   │  ├─ ToolsInventory\              ├─ byte-for-byte clones of Electronics
   │  ├─ ModulesInventory\            ┘  except namespace / sheet / category
   │  ├─ Usage\                       Report, History, HistoryEdit, HistoryDelete
   │  ├─ Damage\                      Report, History, HistoryEdit, HistoryDelete
   │  └─ Shared\_Layout.cshtml        navbar, success-alert slot, footer
   └─ wwwroot\                        css, js, bootstrap + jquery libs
```

Namespace is `InventoryManagement` throughout (it does **not** match the folder name `Inventory_MS`).

---

## 4. Configuration and secrets

`Program.cs` resolves configuration in this order:

- `SpreadsheetId` — env var → `appsettings.json`. **If missing, startup throws.** This is a deliberate fail-fast.
- `GOOGLE_APPLICATION_CREDENTIALS` — env var → configuration. If the path is blank or the file is absent, `GoogleSheetsService` falls back to Application Default Credentials, which is what happens on Cloud Run (the compute default service account).

Two Cloud Run accommodations in `Program.cs`:

```csharp
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");
// app.UseHttpsRedirection();   ← deliberately commented out
```

TLS terminates at Google's front end, so `ForwardedHeadersOptions` trusts `X-Forwarded-Proto` / `X-Forwarded-For` with `KnownNetworks`/`KnownProxies` cleared (i.e. trust any proxy — acceptable only because nothing but Google's LB can reach the container).

The spreadsheet must be shared as **Editor** with whichever service account the app runs as. On Cloud Run that is `<project-number>-compute@developer.gserviceaccount.com`.

---

## 5. The data model — 8 spreadsheet tabs

Row 1 is always the header. Row indexes used in code are **1-based and include the header**, so the item at list position `i` lives on sheet row `i + 1`. That row index is the entity ID that appears in URLs (`?id=14`).

| # | Tab | Cols | Columns in order |
|---|---|---|---|
| 1 | `Master` | 7 | UniqueCode, ComponentName, Category, Brand, Description, Unit, MinStockAlert |
| 2 | `Suppliers` | 2 | SupplierName, ContactInfo |
| 3 | `Electronics_Inventory` | 11 | UniqueCode, ComponentName, Brand, TotalQuantity, Remaining, InvoiceNo, CostPerUnit, TotalCost, Supplier, DateOfPurchase, Remarks |
| 4 | `Electrical_Inventory` | 11 | identical to #3 |
| 5 | `Tools_Inventory` | 11 | identical to #3 |
| 6 | `Modules_Inventory` | 11 | identical to #3 |
| 7 | `Used_Components` | 7 | UniqueCode, ComponentName, Category, BatchPurchaseDate, UsedDate, QuantityUsed, Remarks |
| 8 | `Damaged_Components` | 9 | UniqueCode, ComponentName, Category, BatchPurchaseDate, DamageDate, QuantityDamaged, InvoiceNo, CostPerUnit, Remarks |

Because all four category tabs share one layout, a single `InventoryItem` model serves all of them, with two static maps:

```csharp
SheetNameFor("Electronics") => "Electronics_Inventory"   CodePrefixFor => "E-"
SheetNameFor("Electrical")  => "Electrical_Inventory"    CodePrefixFor => "EL-"
SheetNameFor("Tools")       => "Tools_Inventory"         CodePrefixFor => "T-"
SheetNameFor("Modules")     => "Modules_Inventory"       CodePrefixFor => "M-"
```

Every model follows the same shape: private `const int Col…` indexes, a static `FromRow(IList<object>, int rowIndex)`, and a `ToRow()` returning `List<object>` in column order. `SheetCell` supplies `Cell` / `SafeInt` / `SafeDecimal`, all `InvariantCulture`, all returning empty/zero rather than throwing — necessary because the Sheets API returns numbers as `double` and omits trailing empty cells.

**The category string is the join key for everything.** `SheetNameFor` returns `string.Empty` for an unknown category, and every caller treats empty as "skip silently". A typo in the Category column therefore causes silent data loss rather than an error.

---

## 6. The data layer — `GoogleSheetsService`

Registered as a **singleton** built by hand: `new GoogleSheetsService(spreadsheetId, credentialsPath)`. Public surface is deliberately tiny:

| Method | Behaviour |
|---|---|
| `GetRowsAsync(sheet)` | Reads `{sheet}!A1:Z1000` including the header. Returns empty list, not null. |
| `AppendRowAsync(sheet, row)` | `USER_ENTERED` + `INSERT_ROWS`, returns the new 1-based row index parsed out of the response's `UpdatedRange` (e.g. `"…!A14:K14"` → 14), with a row-count fallback. |
| `UpdateRowAsync(sheet, rowIndex, row)` | Overwrites `A{r}:{letter}{r}`. |
| `DeleteRowAsync(sheet, rowIndex)` | `batchUpdate` / `DeleteDimension ROWS`, `StartIndex = rowIndex - 1` (0-based). Physically removes the row and shifts everything below up. |
| `GetCellValueAsync(...)` | Present but **never called** anywhere. Dead code. |

Design points worth preserving or consciously changing:

- **`SemaphoreSlim _writeLock(1,1)` serialises every write.** It does *not* span a read-then-write sequence, only the individual write call — see §9.5.
- **No sorting in the service.** All ordering is LINQ in the page models. This was an explicit instruction from you in the v2 session.
- **No caching.** Each page load re-reads whole tabs.
- `_sheetIdCache` (a `ConcurrentDictionary`) caches numeric tab IDs, which are needed only for deletes.
- Missing tab produces a clear exception naming the tab.
- `ApplicationName` is still `"Smart-Signage-Inventory"`.

---

## 7. Pages and behaviour

### 7.1 The four category folders are clones

Confirmed by diff: each of `ElectricalInventory`, `ToolsInventory`, `ModulesInventory` differs from `ElectronicsInventory` by only **3 lines per `.cs` file** (namespace, `SheetName` const, `Category` const) and **4–6 lines per `.cshtml`** (model type, page title, headings). That is **16 files holding 4 copies of the same 4 behaviours**. Any category-level change in v2.2 currently has to be made four times, and adding a fifth category means copying a whole folder.

### 7.2 Dashboard (`/`)

Loads all 5 relevant tabs in parallel via `Task.WhenAll`, each wrapped in try/catch so a failing tab degrades to empty rather than breaking the page. Shows six cards: Master count, low-stock count, and a row count per category. Low stock is computed by building a `UniqueCode → MinStockAlert` dictionary from Master, then counting inventory rows where `Remaining < MinStockAlert`. Rows with no Master entry are skipped.

Note: the SRD asks for total stock value on the dashboard; it is not implemented.

### 7.3 Master

Create generates the UniqueCode server-side: scan Master, find the highest numeric suffix for that category's prefix, add one, pad to 3 digits → `E-001`, `EL-014`. Edit shows UniqueCode read-only. Delete removes the row with **no cascade and no reference check**.

### 7.4 Suppliers

Plain CRUD, sorted by name. Slightly different from the other pages: it binds the individual fields rather than a whole `Supplier` object, and round-trips `RowIndex` through a bound property. The build notes flag this as a deliberate fix that must be preserved.

Suppliers exist purely to populate dropdowns. There is no referential integrity — renaming or deleting a supplier does not touch the inventory rows that stored its name as text. The category Edit page compensates by re-inserting an unknown current supplier into its dropdown so the value is not silently lost.

### 7.5 Category inventory — the "Add Stock" upsert

This is the most important behaviour in the system:

1. Load Master items filtered to this category, plus all suppliers.
2. Resolve the selected UniqueCode against Master. If gone, show a friendly error.
3. Scan the category tab for an existing row with that UniqueCode.
4. **If found** — `TotalQuantity += qty`, `Remaining += qty`, then **overwrite** `CostPerUnit` and `Supplier` with the new batch's values, recalculate `TotalCost`, update the row.
5. **If not found** — build a new row seeded from Master (ComponentName, Brand), with `TotalQuantity = Remaining = qty`, and append.

**Invariant: at most one row per UniqueCode per category tab.** Every later lookup depends on it.

Consequence worth understanding before v2.2: because one row must represent all batches, restocking **rewrites history**. `CostPerUnit` and `Supplier` become the latest batch's, `TotalCost` is recomputed as *total* quantity × *latest* cost, while `InvoiceNo`, `DateOfPurchase` and `Remarks` keep the **first** batch's values. So a row can end up claiming the first purchase date alongside the most recent unit cost, and the money value of earlier batches is lost. See §9.2.

Edit makes UniqueCode, ComponentName and Brand read-only and recalculates `TotalCost` server-side. `Remaining` is hand-editable, which is the intended escape hatch for stock corrections.

### 7.6 Report Usage / Report Damage

Both follow the same sequence:

1. Load, per category, only the items with `Remaining > 0`.
2. Validate category, resolve the item, and reject if `quantity > Remaining` with a message naming the actual figure.
3. Snapshot the original row.
4. Decrement `Remaining` and write the inventory row.
5. Append the log row to `Used_Components` / `Damaged_Components`, copying `BatchPurchaseDate` from the inventory row's `DateOfPurchase`.
6. **If the append throws, restore the snapshot and rethrow.** This is the system's consistency mechanism — compensating rollback, since Sheets has no transactions.

Damage additionally captures InvoiceNo and CostPerUnit, each falling back to the inventory row's value when left blank.

`TotalQuantity` is intentionally never decremented — it is the lifetime intake, `Remaining` is current stock.

The Report page renders one `<select>` per category and enables only the matching one via JavaScript, showing available stock as a hint. It works without JS in the sense that the server still validates, but the category/component pairing depends on the script.

### 7.7 Usage / Damage History

Sorted by ComponentName ascending, then date descending. Both support Edit and Delete with **stock reversal**:

- **Edit** — add the original quantity back, re-validate the new quantity against that restored figure, deduct the new quantity, then update the log row. Only quantity, date and remarks are editable; code, name, category and batch date are read-only.
- **Delete** — add the quantity back to the inventory row, then delete the log row.

Both look the inventory row up by UniqueCode and skip the stock adjustment silently if it no longer exists.

---

## 8. Business rules and invariants to preserve

1. Row indexes are 1-based and include the header. `RowIndex` is the entity ID in URLs.
2. `UniqueCode` is immutable once created and is the join key across all tabs.
3. One row per UniqueCode per category tab.
4. Costs are **tax-inclusive**. `TotalCost = round(TotalQuantity × CostPerUnit, 2)`, computed server-side, never accepted from a form. There are no GST columns anywhere — v1 had them and they were deliberately removed.
5. Physical rows are append-only. All ordering happens in memory in the page models, never in the service and never in the sheet.
6. Deletes never renumber. UniqueCode is a real identifier, not a sequence number.
7. Every write goes through `_writeLock`.
8. Money is `decimal`, parsed and formatted with `InvariantCulture`, displayed as `N2` with a `₹` prefix.
9. Stock deduction is followed by a compensating rollback if the log append fails.
10. Dates are stored as plain `yyyy-MM-dd` **strings**, not typed dates.

---

## 9. Findings — issues, risks and debt

Ordered roughly by how much they should influence v2.2 planning.

### 9.1 Verified build state

`dotnet build` → **0 errors, 2 warnings**, both long-known and still unfixed:

| Warning | Location | Fix |
|---|---|---|
| `ASPDEPR005` — `ForwardedHeadersOptions.KnownNetworks` obsolete | `Program.cs:37` | rename to `KnownIPNetworks` |
| `CS0618` — `GoogleCredential.FromFile(string)` obsolete, flagged as a security risk | `GoogleSheetsService.cs:52` | use `CredentialFactory` + `.ToGoogleCredential()` |

Both were diagnosed in the August 12 session but never applied. They are one-line changes.

### 9.2 The batch model is the real architectural limitation

"One row per UniqueCode" and "BatchPurchaseDate" cannot both be true. The usage and damage logs record a `BatchPurchaseDate` copied from the single inventory row, but that row only ever holds the *first* purchase date while its cost reflects the *latest* purchase. So batch attribution in the history tabs is not trustworthy once an item has been restocked at a different price, and the historical value of consumed stock cannot be reconstructed.

If v2.2 cares about cost accuracy or per-batch traceability, this is the thing to change, and it is a schema change, not a UI change.

### 9.3 Security posture

- **No authentication of any kind.** The Cloud Run service is deployed `--allow-unauthenticated`, meaning anyone with the URL can add, edit and delete stock, and there is no record of who did it. This is the single largest risk. `UseAuthorization()` is called without `UseAuthentication()`, which is a no-op. The v1 README already recommends IAP or Google Sign-In; the SRD defers user management to "future".
- **No audit trail.** Edits overwrite cells with no record of the previous value or the actor. Usage and damage events are logged, but corrections to them are not.
- `app.yaml` is a stale App Engine descriptor that contradicts the actual Cloud Run deployment, and it hardcodes the real spreadsheet ID.
- `.gitignore` ignores `service-account-key*.json`, which would **not** match a differently-named key file. The pattern is narrower than the risk.
- No anti-forgery concern in practice (Razor Pages adds token validation to POST handlers by default), but with no auth there is nothing to forge against.

### 9.4 Scale ceilings

- Every read is hardcoded to `A1:Z1000` — a hard ceiling of 999 data rows per tab, and column Z. Silent truncation beyond that.
- No caching at all. The dashboard makes 5 API calls per load, the usage/damage Report pages 4 each, and every list page 1–2. Google's default read quota is 60 requests/minute/user, so a handful of concurrent users clicking around can hit it.
- Full-tab reads mean an O(n) in-memory scan for every lookup by UniqueCode.

### 9.5 Concurrency

`_writeLock` serialises individual writes but **not** read-modify-write sequences. Two users reporting usage of the same component simultaneously can both read `Remaining = 10`, both validate 8 against it, and the second write silently overwrites the first — losing a deduction. The same applies to Add Stock upserts and to UniqueCode generation, where two concurrent Master creates can produce the same code. Low probability at current usage, but it is a correctness gap, not just a performance one.

### 9.6 Data-integrity gaps

- **Master Edit lets you change Category** while UniqueCode stays fixed. Change an item from Electronics to Tools and you get `E-007` sitting in the Tools category with its stock still in `Electronics_Inventory`, invisible to the Tools pages. Nothing warns or migrates.
- **Master Delete does not cascade or check.** Deleting a Master row leaves its inventory and history rows orphaned; the dashboard then stops counting it for low stock (the alert lookup fails) while the stock still exists.
- **No `Remaining ≤ TotalQuantity` enforcement.** Both are hand-editable on the Edit page.
- Supplier names are denormalised text; renames do not propagate.
- Unknown category strings fail silently everywhere.
- `MasterItem.Unit` and `Description` are captured but never displayed on any list page.

### 9.7 Code-level nits

- `UpdateRowAsync` calls `ColumnLetter(row.Count)`, and `ColumnLetter` internally does `index++`. For an 11-column row that produces range `A..L` — one column wider than the 11 values supplied. Harmless today (Sheets writes only the cells provided), but the range and the payload disagree, and column L is never cleared. `GetCellValueAsync` treats the same parameter as 0-based, so the helper is used with two different conventions.
- `Program.cs` contains a commented-out duplicate of the `AddSingleton` line.
- `GetCellValueAsync` is dead code.
- `appsettings.json` has a `GoogleApplicationCredentials` key, but `Program.cs` reads `GOOGLE_APPLICATION_CREDENTIALS`. Config keys are case-insensitive but not underscore-insensitive, so **that JSON key does nothing** — only the environment variable works.
- The project `README.md` still documents v1 App Engine deployment and the old branding.
- No tests exist, and no test project is set up.
- No search, filter, pagination or export on any list page.

---

## 10. History: how the project got here

**v1 — "Smart Signage Board — Inventory Management"** (.NET 8, Sheets package 1.68): 4 tabs (`PCB_Inventory` 13 cols, `Tools_Inventory`, `Panel_Inventory`, `Damages_Components`), three separate models, an `Sl.No.` column renumbered on every delete, GST at 18% on PCB items only, damage reporting only, and read-only damage history.

**v2 — the August 12 transformation** replaced essentially everything:

| Change | v1 → v2 |
|---|---|
| Categories | PCB / Panel / Tools → Electronics / Electrical / Tools / Modules |
| Identity | `Sl.No.`, renumbered on delete → `UniqueCode`, immutable, never renumbered |
| Catalogue | items created directly in inventory tabs → central `Master` tab feeding everything |
| Suppliers | free-text field → dedicated tab + dropdowns |
| Tax | BaseCost + 18% GST + TotalCost → single tax-inclusive `CostPerUnit` |
| Usage | did not exist → full report + history with edit/delete and reversal |
| Damage history | read-only → editable with stock reversal |
| Stock entry | Create → **AddStock** upsert |
| Low stock | global hardcoded threshold of 5 → per-item `MinStockAlert` in Master |
| Models | 5 category classes → 1 generic `InventoryItem` + `SheetCell` helpers |
| Framework | net8.0 → net10.0 |

Your instructions during that session that still constrain the codebase: modify in place rather than starting a new project; keep namespaces and the service-layer pattern; no sorting in the service; no GST columns; a single generic inventory model; and **you handle build, test and deployment yourself** — which is why the two obsolete warnings were diagnosed but left unapplied, and why deployment moved to Cloud Run outside the code sessions.

Deployment also drifted: the docs describe App Engine (`asia-south1`, Secret Manager, `app.yaml`), but the actual deployment is Cloud Run (`us-central1`, env var, ADC). App Engine Standard does not support .NET and Flexible is expensive, which is why Cloud Run won.

---

## 11. Open questions for v2.2

Things I would want decided before writing code:

1. **Authentication** — is it in scope for 2.2? If yes, Google Sign-In with an allowed-users tab (the SRD's suggestion) or IAP in front of Cloud Run?
2. **Batch tracking** — do you need per-batch cost and traceability, or is "latest cost wins" acceptable? This determines whether the schema changes.
3. **The 4× duplicated category pages** — consolidate into one parameterised set of pages (`/Inventory/{category}`) now, or leave them and keep paying 4× on every change?
4. **Branding** — settle on GIMS or Smart Signage Inventory, and update the navbar, `ApplicationName` and README together.
5. **Scale** — is 999 rows per tab going to bind soon? If so, caching and paging matter more than features.
6. **Dashboard stock value** — the SRD asks for it and the data exists; add it?
7. **Search / filter / export** — repeatedly listed as future work. Priority for 2.2?

---

## 12. Coverage checklist

- [x] Project structure and build system
- [x] Startup, DI, configuration and secret resolution
- [x] All 6 models and their column mappings
- [x] The complete data-access layer
- [x] All 33 page models and views
- [x] Layout, navigation and shared partials
- [x] Full 8-tab schema
- [x] All business rules, invariants and rollback flows
- [x] `claude_works` documentation (8 files)
- [x] The v2 build session transcript (1.9 MB)
- [x] v1 → v2 change history and the reasoning behind it
- [x] Build verification (0 errors, 2 warnings)
- [ ] Runtime behaviour against the live spreadsheet — not executed
- [ ] Cloud Run deployment state — not inspected
- [ ] `Inventory_MS_git`, git history, zip archives, other transcripts — out of scope by instruction
