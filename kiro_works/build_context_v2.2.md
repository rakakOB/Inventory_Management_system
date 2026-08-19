# IMS v2.2 — Build Context

**Project:** Inventory Management System (IMS / GIMS)
**Version:** 2.2
**Date:** 2026-08-18
**Root:** `C:\e\Projects\Inventory_Management\Inventory_MS\Inventory_MS`
**Build state:** `dotnet build` → **0 errors, 2 warnings** (both pre-existing, §11)

This is the handoff document for v2.2: everything needed to build, configure, run,
test and deploy it without the conversation that produced it. Companion documents
in `kiro_works\`: `initial_study.md` (the v2.0 baseline analysis) and
`v2.2_changes.md` (the per-requirement change record).

---

## 1. Project facts

| | |
|---|---|
| Framework | ASP.NET Core Razor Pages, `net10.0` |
| SDK used | .NET 10.0.400 |
| Namespace | `InventoryManagement` (does not match the folder name `Inventory_MS`) |
| Data store | Google Sheets API v4 — **9 tabs**, no SQL database |
| Packages | `Google.Apis.Sheets.v4` 1.75.0.4178 · `Microsoft.AspNetCore.Authentication.Google` 10.0.11 |
| UI | Bootstrap 5.3.3 (CDN) + `wwwroot/css/site.css`, light/dark via `data-bs-theme` |
| App auth | Google OAuth → cookie session, allow-list held in the spreadsheet |
| Hosting | Cloud Run, service `inventory-ms`, region `us-central1`, `--allow-unauthenticated` |
| Files | 8 models · 3 services · 45 `.cshtml` · 39 `.cshtml.cs` |

---

## 2. What v2.2 changed, in one page

1. **Authentication added.** Google OAuth with a cookie session. Email checked
   against a new `AllowedUsers` tab before the cookie is issued. Every page
   requires sign-in.
2. **The "one row per UniqueCode per category sheet" invariant is gone.** Add
   Stock now appends a new row per purchase, so a category tab holds one row per
   **batch** and several rows can share a UniqueCode.
3. **Usage and damage are reported against a specific batch**, selected from a
   dropdown and addressed by RowIndex.
4. **`/Reports`** — live CSV export of every sheet, plus a ZIP of all of them.
5. **`/Settings`** — account details, sign-out, light/dark theme switcher.
6. **Sorting is by UniqueCode**, not ComponentName (Suppliers excepted).
7. **Low-stock moved** from a dashboard card to highlighted rows on the category
   pages.
8. **Navbar regrouped** into two dropdowns; responsive header layouts fixed.
9. **Renamed** to "Inventory Management System" throughout.

---

## 3. Google Sheets schema — 9 tabs

Row 1 is the header. Row indexes in code are **1-based and include the header**,
so list position `i` is sheet row `i + 1`. That index is the entity ID in URLs.

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
| 9 | **`AllowedUsers`** | 1 | **Email** ← new in v2.2 |

**No column order changed.** The only schema work required is adding tab 9.

Category mapping (static on `InventoryItem`):

```
Electronics -> Electronics_Inventory   prefix E-
Electrical  -> Electrical_Inventory    prefix EL-
Tools       -> Tools_Inventory         prefix T-
Modules     -> Modules_Inventory       prefix M-
```

A category string that matches none of these resolves to `string.Empty` and every
caller treats that as "skip silently" — a typo in the Category column loses data
without raising an error. Unchanged from v2.0, still worth knowing.

---

## 4. Code map

```
Inventory_MS/
├─ Program.cs                     config, auth, DI, pipeline, Cloud Run binding
├─ appsettings.json               SpreadsheetId + OAuth placeholders (commented)
├─ app.yaml                       STALE App Engine descriptor, not used
├─ README.md                      setup + deployment, rewritten for v2.2
├─ Models/
│  ├─ SheetCell.cs                Cell / SafeInt / SafeDecimal, InvariantCulture
│  ├─ MasterItem.cs               7 cols
│  ├─ Supplier.cs                 2 cols
│  ├─ InventoryItem.cs            11 cols, shared by all four category tabs,
│  │                              SheetNameFor / CodePrefixFor / RecalculateCosts
│  ├─ UsedItem.cs                 7 cols
│  ├─ DamagedItem.cs              9 cols
│  ├─ AllowedUser.cs              NEW — 1 col, carries the deployment note
│  └─ UniqueCodeComparer.cs       NEW — natural ordering for UniqueCode
├─ Services/
│  ├─ GoogleSheetsService.cs      the whole data layer, singleton, _writeLock
│  ├─ AccessControlService.cs     NEW — AllowedUsers lookup, cached, fails closed
│  └─ StockAlerts.cs              NEW — Master thresholds + low-stock codes
└─ Pages/
   ├─ Index                       dashboard
   ├─ Login / AccessDenied / Logout      NEW — anonymous
   ├─ Reports / Settings                 NEW — authenticated
   ├─ Error                              now [AllowAnonymous]
   ├─ Master/                     Index, Create, Edit, Delete
   ├─ Suppliers/                  Index, Create, Edit, Delete
   ├─ ElectronicsInventory/       Index, AddStock, Edit, Delete
   ├─ ElectricalInventory/        ┐
   ├─ ToolsInventory/             ├─ clones differing only in namespace,
   ├─ ModulesInventory/           ┘  SheetName, Category and page titles
   ├─ Usage/                      Report, History, HistoryEdit, HistoryDelete
   ├─ Damage/                     Report, History, HistoryEdit, HistoryDelete
   └─ Shared/
      ├─ _Layout.cshtml           navbar with dropdowns, auth controls
      ├─ _AuthLayout.cshtml       NEW — navbar-less, for anonymous pages
      ├─ _ThemeHead.cshtml        NEW — inline early-apply theme script
      ├─ _ValidationScriptsPartial.cshtml
      └─ _Layout.cshtml.css       DEAD CODE — see §12
```

The four category folders remain 4× duplicated. Consolidating them into one
parameterised route was raised in `initial_study.md` §11 and was **not** part of
v2.2, so any category-level change still has to be made four times.

---

## 5. Authentication

### Flow

```
unauthenticated request
   └─> 302 to accounts.google.com  (DefaultChallengeScheme = Google)
        └─> user consents
             └─> /signin-google
                  └─> OnTicketReceived: email claim vs AllowedUsers tab
                       ├─ listed     -> persistent cookie, on to the target page
                       └─ not listed -> log, SignOutAsync, 302 /AccessDenied?email=…
```

- `DefaultScheme` = cookie, `DefaultChallengeScheme` = Google. An unauthenticated
  visit therefore goes **straight to Google's consent screen**. `/Login` exists as
  an explicit entry point and as the landing page when consent fails or is
  cancelled (`OnRemoteFailure`). To show `/Login` first instead, set
  `DefaultChallengeScheme` to the cookie scheme — one line.
- The allow-list check happens in `OnTicketReceived`, **before** the cookie is
  written, so an unlisted Google account never holds a session.
- Cookie: name `ims.auth`, HttpOnly, SameSite=Lax, `SecurePolicy = SameAsRequest`,
  `IsPersistent = true`, 14-day expiry, sliding.
- `AuthorizationOptions.FallbackPolicy = RequireAuthenticatedUser()` protects
  every Razor Page. `[AllowAnonymous]` on `Login`, `AccessDenied`, `Logout`,
  `Error`. `Error` **must** stay anonymous or a pre-auth failure would redirect
  instead of reporting itself.
- `UseAuthentication()` sits immediately before the existing `UseAuthorization()`.
  Without it, `UseAuthorization()` was a no-op — which is what v2.0 shipped.
- `UseStaticFiles()` runs before the auth middleware, so the sign-in page can load
  its stylesheet.
- `/signin-google` is handler middleware, not a Razor Page, so the fallback policy
  does not block the callback.

### AccessControlService

Reads `AllowedUsers` via `GoogleSheetsService.GetRowsAsync`, caches the set for
**2 minutes** behind a `SemaphoreSlim` with a double-check, and **fails closed**:
a missing tab or an API error denies everyone and is not cached. An empty tab is
logged as a warning. `Invalidate()` exists for a future "reload users" action.

### Startup refuses to run unconfigured

`Program.cs` throws if `SpreadsheetId`, `ClientId` or `ClientSecret` is missing,
with a message naming exactly what to set. The app will not start and serve the
inventory unprotected.

---

## 6. Business rules and invariants

**Still true:**

1. Row indexes are 1-based and include the header; `RowIndex` is the URL id.
2. `UniqueCode` is immutable once created and joins every tab.
3. Costs are tax-inclusive. `TotalCost = round(TotalQuantity × CostPerUnit, 2)`,
   computed server-side, never taken from the form. No GST columns.
4. Physical rows are append-only. All ordering is LINQ in the page models — never
   in the service, never in the sheet.
5. Deletes never renumber.
6. Every write goes through `GoogleSheetsService._writeLock`.
7. Money is `decimal`, `InvariantCulture`, displayed `N2` with `₹`.
8. Stock deduction is followed by a compensating rollback if the log append fails.
9. Dates are plain `yyyy-MM-dd` **strings**, not typed dates.

**Retired in v2.2:**

10. ~~One row per UniqueCode per category sheet.~~ A category tab now holds one
    row per **batch**; several rows may share a UniqueCode. Anything acting on a
    single batch addresses it by `RowIndex`.

---

## 7. Page behaviour

### Dashboard `/`
Loads Master + 4 category tabs in parallel, each in its own try/catch so a failing
tab degrades to empty. Five cards: Master count, then per category the distinct
**component** count with batch-row count and total remaining units beneath it. The
"Low Stock Items" card was removed.

### Master
Create generates the code server-side: highest numeric suffix for that category's
prefix, plus one, padded to three digits (`E-001`). Edit keeps UniqueCode
read-only. Delete removes the row with no cascade and no reference check —
unchanged, and still a way to orphan inventory and history rows.

Sorted by **UniqueCode ascending**.

### Suppliers
Plain CRUD, still sorted by **SupplierName** because there is no UniqueCode to
sort on. Binds individual fields and round-trips `RowIndex` through a bound
property — a deliberate deviation from the other pages; preserve it. Supplier
names are denormalised text, so renames do not propagate.

### Category inventory (×4)
**Add Stock always appends.** The new row gets UniqueCode and ComponentName from
Master, `Brand` from Master or `-`, `TotalQuantity = Remaining = Quantity`,
invoice / cost / supplier / date / remarks as entered, and
`TotalCost = round(Quantity × CostPerUnit, 2)`.

Index sorted by **UniqueCode ascending, then DateOfPurchase ascending** so a new
batch lands directly beneath the earlier batches of the same component.

Edit keeps UniqueCode, ComponentName and Brand read-only and recalculates
TotalCost. `Remaining` stays hand-editable as the stock-correction escape hatch.

**Low-stock highlighting.** Rows carry `class="low-stock"` plus a badge. The
threshold is compared against the **total remaining across all batches of a
UniqueCode**, not the individual row — judged per row, every nearly-exhausted
batch would light up even with a full batch beneath it. Fallback threshold is 5.
Note `SafeInt` cannot tell a blank cell from a literal `0`, so `MinStockAlert = 0`
is treated as missing and becomes 5; "never alert for this item" is not currently
expressible.

Cost: this adds one extra Master read per category page load.

### Report Usage / Report Damage
Dropdown lists **individual batch rows**:

```
E-001 – 10k Resistor (Date: 2026-07-23, Remaining: 10)
```

Options are ordered UniqueCode ascending then DateOfPurchase ascending, so taking
the first match consumes oldest stock first.

Option value is **`rowIndex|uniqueCode`**. The RowIndex drives the deduction; the
code is re-checked against the row found after the fresh read. RowIndex alone is
unsafe because deleting any row above shifts everything below it up, so a stale
index can point at a different component. On mismatch the submission is refused
with an explanation rather than deducting from the wrong component.

Sequence: validate category → resolve batch → reject if quantity > that batch's
Remaining → snapshot row → decrement and write → append the log row with
`BatchPurchaseDate = item.DateOfPurchase` → **on append failure, restore the
snapshot and rethrow**.

Damage additionally captures InvoiceNo and CostPerUnit, each falling back to the
batch row's value when blank.

`TotalQuantity` is never decremented; it is lifetime intake.

### Usage / Damage History
Sorted **UniqueCode ascending, then date descending**. Edit allows quantity, date
and remarks only; code, name, category and batch date are read-only.

Stock reversal now targets the right batch. Log rows hold no pointer to the
inventory row, only UniqueCode and BatchPurchaseDate, so `FindByCode` was replaced
with **`FindBatch(rows, code, batchPurchaseDate)`**, matching on both and falling
back to the first row with the same code when the original batch row is gone.

- **Edit** — add the original quantity back, re-validate the new quantity against
  that restored figure, deduct, then update the log row.
- **Delete** — add the quantity back, then delete the log row.

### Reports `/Reports`
Eight downloads (Master, four categories, Suppliers, Usage, Damage) plus
**Download All (ZIP)**. Handlers: `?handler=Csv&key=…` and `?handler=Zip`.

- Read live per click; nothing cached or precomputed.
- All columns plus the header row. Ragged rows — the Sheets API drops trailing
  empty cells — are padded to the widest row so columns stay aligned.
- Fields quoted only when they contain a comma, quote, newline or edge whitespace;
  embedded quotes doubled.
- **UTF-8 with BOM**, without which Excel mangles `₹` and other non-ASCII text.
- The key resolves through an allow-list, so the handler cannot be pointed at an
  arbitrary tab.
- In the ZIP a missing tab is skipped rather than failing the whole archive.
- The `ZipArchive` is disposed before `buffer.ToArray()` — otherwise the central
  directory is unflushed and the file is unreadable.

Filenames: `<SheetName>_<yyyy-MM-dd>.csv`, archive `InventoryReports_<date>.zip`.

### Settings `/Settings`
Name and email from the Google profile claims, sign-out, light/dark selector with
a live preview, and a placeholder card for future preferences.

Theme is stored in `localStorage` **and** mirrored to a cookie, then applied by an
inline `<head>` script (`_ThemeHead.cshtml`) before first paint so there is no
flash of the wrong theme. Implemented with Bootstrap 5.3's `data-bs-theme`.

### Navigation
```
Home · Inventory ▾ (Electronics, Electrical, Tools, Modules)
     · Transactions ▾ (Report Usage, Usage History | Report Damage, Damage History)
     · Master · Suppliers · Reports        [right:] Settings · Sign out
```
Active state highlights the parent dropdown when any child is open. The navbar
stays dark in both themes via `data-bs-theme="dark"` on the `<nav>`, since
`navbar-dark` is deprecated in Bootstrap 5.3. Sign-out is a POST form, so a stray
link or prefetch cannot end a session.

Headers that pair a heading with action buttons stack below the `sm` breakpoint
with full-width buttons: dashboard, all four category indexes, Master, Suppliers,
both histories, both report forms.

---

## 8. Configuration

| Setting | Where from | Required |
|---|---|---|
| `SpreadsheetId` | env var → appsettings → user secrets | Yes — startup throws |
| `Authentication:Google:ClientId` / env `GoogleClientId` | config or env | Yes — startup throws |
| `Authentication:Google:ClientSecret` / env `GoogleClientSecret` | config or env | Yes — startup throws |
| `GOOGLE_APPLICATION_CREDENTIALS` | env var → config | No — falls back to ADC |
| `PORT` | env var (Cloud Run) | No — defaults to 8080 |

The OAuth client is **not** the service account. It is a separate OAuth 2.0 *Web
application* client in the same project.

`appsettings.json` was corrected in v2.2: the key is now the underscored
`GOOGLE_APPLICATION_CREDENTIALS`, matching what `Program.cs` reads. The old
`GoogleApplicationCredentials` spelling never resolved — config keys are
case-insensitive but not underscore-insensitive, so it was silently dead.

---

## 9. Setup and run

### Prerequisites before the app will work

1. **Add the `AllowedUsers` tab** with header `Email` in row 1 and one address per
   row. An empty or unreadable tab denies everyone — it fails closed by design.
2. **Share the spreadsheet as Editor** with the service account the app runs as.
   On Cloud Run that is the Compute Engine default,
   `<project-number>-compute@developer.gserviceaccount.com`. Sharing the
   spreadsheet covers all nine tabs; no separate grant for `AllowedUsers`.
3. **Create an OAuth 2.0 Web application client** and register
   `<base-url>/signin-google` as an authorised redirect URI for every host:
   ```
   https://localhost:62251/signin-google
   https://inventory-ms-xxxxxxxx-uc.a.run.app/signin-google
   ```
   Consent screen scopes: `email`, `profile`.

### Local

```powershell
cd C:\e\Projects\Inventory_Management\Inventory_MS\Inventory_MS
dotnet user-secrets set SpreadsheetId "<spreadsheet-id>"
dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>"
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\path\to\service-account-key.json"
dotnet build      # expect 0 errors, 2 warnings
dotnet run
```

### Cloud Run

```powershell
gcloud run deploy inventory-ms --source . --region us-central1 `
  --allow-unauthenticated `
  --set-env-vars SpreadsheetId=<spreadsheet-id> `
  --set-env-vars GoogleClientId=<client-id> `
  --set-env-vars GoogleClientSecret=<client-secret>
```

`--allow-unauthenticated` is still correct: Cloud Run admits the request and the
application enforces sign-in. Prefer `--set-secrets` (Secret Manager) for the
client secret. Forwarded headers are trusted, so the OAuth redirect URI is
generated as `https` even though the container sees plain HTTP.

---

## 10. Test checklist

**Auth**
1. Visit `/` signed out → lands on Google's consent screen.
2. Sign in with an email **in** `AllowedUsers` → dashboard, navbar shows Settings
   and Sign out.
3. Sign in with an email **not** in `AllowedUsers` → Access Denied naming the
   address, and no session cookie survives.
4. Restart the browser → still signed in (persistent cookie).
5. Sign out → back to sign-in; `/Master` is no longer reachable.
6. Remove your address from `AllowedUsers`, wait 2 minutes, sign in again → denied.

**Batches**
7. Add Stock twice for the same component at different costs and dates → **two
   rows**, each with its own cost and TotalCost. Nothing overwritten.
8. Category index groups both rows under the code, oldest purchase first.
9. `/Usage/Report` lists both batches separately with their dates and remaining.
10. Report usage against the **second** batch → only that row's Remaining drops.
11. Usage History edit the quantity → the same batch adjusts, not the first row
    with that code.
12. Usage History delete → quantity returns to that batch.
13. Repeat 9–12 for damage.

**Low stock**
14. Set MinStockAlert above the combined remaining of a component → every row of
    that component highlights with a badge.
15. Add a fresh batch pushing the total above the threshold → highlight clears.

**Reports**
16. Each Download CSV opens the right file with headers and all columns.
17. Non-ASCII text (`₹`, remarks) renders correctly when opened in Excel.
18. A field containing a comma stays in one column.
19. Download All returns a ZIP containing eight CSVs.

**UI**
20. Desktop: navbar is even, dropdowns work, active page highlights its parent.
21. Phone: hamburger opens, every link reachable, dashboard buttons full width and
    not squeezed beside the heading.
22. Settings → Dark → applies immediately; reload shows no flash of light theme;
    tables, headers and low-stock rows all legible.

---

## 11. Known warnings

Both predate v2.2 and are unchanged. Left alone because they were outside the
brief and one sits in the forwarded-headers block adjacent to the Cloud Run
configuration that must not be touched.

| Warning | Location | One-line fix |
|---|---|---|
| `ASPDEPR005` `ForwardedHeadersOptions.KnownNetworks` obsolete | `Program.cs:164` | rename to `KnownIPNetworks` |
| `CS0618` `GoogleCredential.FromFile(string)` obsolete, flagged as a security risk | `GoogleSheetsService.cs:52` | `CredentialFactory` + `.ToGoogleCredential()` |

---

## 12. Do not touch

- **`Program.cs` Cloud Run specifics** — the `PORT` env var with
  `builder.WebHost.UseUrls(...)`, and `app.UseHttpsRedirection()` staying
  commented out. Removing or uncommenting either breaks the deployment.
- **`GoogleSheetsService._writeLock`** and the service's public API.
- **Sheet column order** in all nine tabs.
- **No sorting in the service** — it belongs in the page models.
- **`Suppliers/Edit` `RowIndex` binding**, a deliberate deviation from the pattern.
- **`app.yaml`** is a stale App Engine descriptor that contradicts the Cloud Run
  deployment and hardcodes the spreadsheet ID. It is not used. Delete it or bring
  it up to date, but do not treat it as the source of truth.
- **`Pages/Shared/_Layout.cshtml.css`** is dead: it is a CSS-isolation file, but no
  `Inventory_MS.styles.css` link exists in any layout, so it never loads. It
  contains hardcoded light-mode colours that would fight dark mode if it were ever
  wired up.

---

## 13. Verified vs unverified

**Verified in this build**
- `dotnet build` — 0 errors, 2 warnings.
- App boots with credentials supplied.
- `/Login` and `/AccessDenied` render anonymously (200).
- `/`, `/Master/Index`, `/Usage/Report`, `/Reports`, `/Settings` all return 302 to
  `accounts.google.com` when signed out.
- Non-ASCII characters (`₹`, en dash) survived the generation of the three cloned
  category folders — checked by codepoint count, not by eye.

**Not verified**
- Nothing was exercised against the real spreadsheet. CSV/ZIP contents, the
  `AllowedUsers` lookup and the batch deduction were verified by reading code and
  by booting with dummy credentials, not by round-tripping live data.
- The Google consent flow end to end — needs a real OAuth client.
- Cloud Run deployment of this revision.

---

## 14. Carried forward, still open

From `initial_study.md` §11, not addressed in v2.2:

1. **The four category folders are still 4× duplicated** — 16 files, four copies of
   four behaviours. Consolidating into `/Inventory/{category}` would make a fifth
   category trivial and stop every change costing 4×.
2. **No audit trail.** Edits overwrite cells with no record of who changed what.
   Now that sign-in exists, the identity needed to write one is finally available.
3. **Read-modify-write races.** `_writeLock` serialises individual writes but not
   read-then-write sequences, so two people deducting from the same batch
   simultaneously can lose one deduction. Also applies to Add Stock and to
   UniqueCode generation.
4. **Scale ceiling.** Every read is `A1:Z1000` — roughly 999 data rows per tab —
   and there is no caching. Batch rows now accumulate faster than before, so this
   ceiling arrives sooner than it did in v2.0.
5. **Master Edit still lets Category change** while UniqueCode stays fixed,
   stranding stock in the old category's sheet.
6. **Master Delete still has no cascade or reference check.**
7. **Dashboard total stock value** — the SRD asks for it, the data exists.
8. **Search, filter and pagination** on the list pages.
