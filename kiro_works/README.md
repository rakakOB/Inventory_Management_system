# I2ST — Inventory Management System (IMS v2.3)

> **Organisation:** I2ST Technologies Pvt. Ltd.
> **Version:** 2.3
> **Framework:** ASP.NET Core Razor Pages, .NET 10
> **Database:** Google Sheets API v4 (service account)
> **Deployment:** Google Cloud Run (serverless)
> **Authentication:** Google OAuth 2.0 + allow-list

---

## Table of Contents

1. [Overview](#1-overview)
2. [Features](#2-features)
3. [Tech Stack](#3-tech-stack)
4. [Architecture](#4-architecture)
5. [Google Sheets Schema](#5-google-sheets-schema)
6. [Project Structure](#6-project-structure)
7. [Core Business Logic](#7-core-business-logic)
8. [Authentication & Access Control](#8-authentication--access-control)
9. [Deployment — Google Cloud Run](#9-deployment--google-cloud-run)
10. [Local Development](#10-local-development)
11. [Configuration Reference](#11-configuration-reference)
12. [Version History](#12-version-history)

---

## 1. Overview

The **I2ST Inventory Management System (IMS)** is a production web application
for tracking electronics, electrical components, tools and modules used
internally by I2ST Technologies Pvt. Ltd. It manages **stock in** (purchases),
**stock out** (usage and damage), and provides real-time visibility into
remaining quantities, batch-level cost tracking and low-stock alerts.

There is no traditional SQL database. A **Google Spreadsheet is the live data
store**, accessed through the Google Sheets API v4. The server-side application
reads raw cells, parses them into typed C# models, performs all business logic
(stock maths, validation, compensating rollback) in memory, and writes changes
back to the spreadsheet.

This design was chosen deliberately: it costs nothing to host at low scale,
needs no database administration, and lets authorised staff inspect or export
the raw data directly in Google Sheets.

---

## 2. Features

### 2.1 Authentication & Access Control
- **Google OAuth 2.0** sign-in — only users listed in the `AllowedUsers`
  spreadsheet tab can access the application.
- Persistent session cookie with 14-day sliding expiry.
- Access Denied page with clear feedback for unlisted accounts.
- Fail-closed design: a missing or unreadable allow-list denies everyone.

### 2.2 Master Component Catalogue
- Central registry of all tracked components with auto-generated unique codes
  (`E-001`, `EL-014`, `T-003`, `M-002`).
- Category classification: Electronics, Electrical, Tools, Modules.
- Per-component minimum stock alert threshold.
- Full CRUD (Create, Edit, Delete).

### 2.3 Supplier Management
- Dedicated supplier list feeding dropdown selections across the app.
- Full CRUD with alphabetical ordering.

### 2.4 Multi-Batch Inventory Tracking
- **Each stock purchase is recorded as a separate batch row** — cost, invoice,
  supplier and purchase date are preserved per batch, enabling accurate
  historical cost analysis.
- Four category inventories (Electronics, Electrical, Tools, Modules) sharing an
  identical 11-column schema.
- Automatic total cost calculation: `TotalCost = Quantity × CostPerUnit`.
- Edit and delete at the batch level.

### 2.5 Usage Reporting & History
- Report component usage against a **specific batch** — the dropdown shows each
  batch row with its date and remaining quantity.
- Automatic stock deduction with **compensating rollback** if the log write fails.
- Full usage history with edit and delete, both of which reverse and re-apply the
  stock adjustment transactionally.

### 2.6 Damage Reporting & History
- Report damaged components with optional per-event invoice and unit cost capture.
- Same batch-level selection and rollback-protected deduction as usage.
- Full damage history with edit and delete.

### 2.7 Low-Stock Alerts
- Each category inventory page highlights components whose **combined remaining
  stock across all batches** falls below the Master-defined threshold.
- Visual row highlighting with a "Low stock" badge — immediately actionable where
  stock is managed, not buried in a dashboard number.

### 2.8 Reports & CSV Export
- Live data export: download any sheet as a properly formatted CSV file.
- Sheets available: Master, all four category inventories, Suppliers, Usage
  History, Damage History.
- **Download All** button generates a ZIP archive of every CSV.
- UTF-8 with BOM — renders correctly in Microsoft Excel without import steps.

### 2.9 Dark / Light Theme
- One-click theme switcher (Settings page).
- Persists via localStorage and cookie — no flash of wrong theme on page load.
- Works across all pages including tables, cards, forms and navigation.

### 2.10 Responsive UI
- Grouped navigation: Inventory and Transactions as dropdowns, reducing navbar
  clutter.
- Mobile-first responsive design: hamburger menu, stacking buttons on small
  screens, full-width actions.
- Professional corporate styling: dark navbar, clean card layouts, consistent
  spacing.

### 2.11 Cloud-Native Deployment
- Deploys to Google Cloud Run (serverless, scales to zero).
- Application Default Credentials — no key file needed in production.
- TLS terminated at Google's load balancer; app handles forwarded headers.

---

## 3. Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core Razor Pages, .NET 10 |
| Data store | Google Sheets API v4 (`Google.Apis.Sheets.v4` 1.75.0.4178) |
| Authentication | Google OAuth 2.0 (`Microsoft.AspNetCore.Authentication.Google` 10.0.11) |
| UI | Bootstrap 5.3.3 (CDN), custom CSS with `data-bs-theme` for dark mode |
| Hosting | Google Cloud Run, `us-central1` |
| Source control | GitHub |

---

## 4. Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Browser (User)                        │
└────────────────────────┬────────────────────────────────┘
                         │ HTTPS
┌────────────────────────▼────────────────────────────────┐
│           Google Cloud Run (terminates TLS)              │
│  ┌────────────────────────────────────────────────────┐ │
│  │  ASP.NET Core Razor Pages (.NET 10)                │ │
│  │  ┌──────────────────────────────────────────────┐  │ │
│  │  │  Cookie Auth + Google OAuth Middleware        │  │ │
│  │  ├──────────────────────────────────────────────┤  │ │
│  │  │  Page Models (business logic, validation)    │  │ │
│  │  ├──────────────────────────────────────────────┤  │ │
│  │  │  GoogleSheetsService (singleton, _writeLock) │  │ │
│  │  └──────────────────────────────────────────────┘  │ │
│  └─────────────────────┬──────────────────────────────┘ │
└────────────────────────┼────────────────────────────────┘
                         │ Google Sheets API v4 (OAuth2 token)
┌────────────────────────▼────────────────────────────────┐
│           Google Spreadsheet (9 tabs)                    │
│  Master | Suppliers | Electronics_Inventory | ...        │
│  Used_Components | Damaged_Components | AllowedUsers     │
└─────────────────────────────────────────────────────────┘
```

**Request lifecycle:**
1. Browser hits a page URL.
2. Cloud Run's load balancer terminates TLS, forwards plain HTTP with
   `X-Forwarded-*` headers.
3. Authentication middleware verifies the session cookie; unauthenticated
   requests are challenged to Google's consent screen.
4. Page model injects `GoogleSheetsService`, reads the relevant tab(s), parses
   rows into typed models, applies business logic.
5. Writes (append, update, delete) are serialised through a `SemaphoreSlim` lock
   so concurrent requests cannot interleave on the same spreadsheet.
6. The page renders the Razor view or redirects with a success message.

---

## 5. Google Sheets Schema

Nine tabs. Header row is row 1. All columns in the exact order below.

### 5.1 `Master` — Component Catalogue (7 columns)

| Col | Name | Notes |
|---|---|---|
| A | UniqueCode | Auto-generated: `E-001`, `EL-014`, `T-003`, `M-002` |
| B | ComponentName | Primary identifier |
| C | Category | `Electronics` / `Electrical` / `Tools` / `Modules` |
| D | Brand | Optional |
| E | Description | Optional |
| F | Unit | e.g. `pcs`, `meters`, `sets` |
| G | MinStockAlert | Low-stock threshold (default 5) |

### 5.2 `Suppliers` (2 columns)

| Col | Name |
|---|---|
| A | SupplierName |
| B | ContactInfo |

### 5.3 Category Inventory Tabs (11 columns each)

`Electronics_Inventory`, `Electrical_Inventory`, `Tools_Inventory`,
`Modules_Inventory` — identical layout:

| Col | Name | Notes |
|---|---|---|
| A | UniqueCode | FK → Master |
| B | ComponentName | From Master |
| C | Brand | From Master |
| D | TotalQuantity | This batch's purchased quantity |
| E | Remaining | Current stock in this batch |
| F | InvoiceNo | |
| G | CostPerUnit | Tax-inclusive (₹) |
| H | TotalCost | `= TotalQuantity × CostPerUnit` |
| I | Supplier | From Suppliers dropdown |
| J | DateOfPurchase | `yyyy-MM-dd` |
| K | Remarks | |

**Multiple rows may share a UniqueCode** — each row is one purchase batch.

### 5.4 `Used_Components` — Usage Log (7 columns)

| Col | Name |
|---|---|
| A | UniqueCode |
| B | ComponentName |
| C | Category |
| D | BatchPurchaseDate |
| E | UsedDate |
| F | QuantityUsed |
| G | Remarks |

### 5.5 `Damaged_Components` — Damage Log (9 columns)

| Col | Name |
|---|---|
| A | UniqueCode |
| B | ComponentName |
| C | Category |
| D | BatchPurchaseDate |
| E | DamageDate |
| F | QuantityDamaged |
| G | InvoiceNo |
| H | CostPerUnit |
| I | Remarks |

### 5.6 `AllowedUsers` — Access Control (1 column)

| Col | Name |
|---|---|
| A | Email |

Only the Google accounts listed here may sign in.

---

## 6. Project Structure

```
Inventory_MS/
├─ Inventory_MS.csproj          .NET 10, nullable + implicit usings
├─ Program.cs                   Startup, auth, DI, Cloud Run binding
├─ appsettings.json             Config placeholders (secrets via env vars)
├─ README.md                    This file
├─ Models/
│  ├─ AllowedUser.cs            Access control model
│  ├─ DamagedItem.cs            Damage log row
│  ├─ InventoryItem.cs          Category inventory row (all four tabs)
│  ├─ MasterItem.cs             Master catalogue row
│  ├─ SheetCell.cs              Safe cell-parsing helpers
│  ├─ Supplier.cs               Supplier row
│  ├─ UniqueCodeComparer.cs     Natural-order code sorting
│  └─ UsedItem.cs               Usage log row
├─ Services/
│  ├─ AccessControlService.cs   AllowedUsers lookup (cached, fail-closed)
│  ├─ GoogleSheetsService.cs    All Sheets API calls (singleton, write lock)
│  └─ StockAlerts.cs            Low-stock threshold logic
├─ Pages/
│  ├─ Index                     Dashboard
│  ├─ Login / AccessDenied / Logout
│  ├─ Reports                   CSV / ZIP export
│  ├─ Settings                  Account, theme, preferences
│  ├─ Master/                   CRUD
│  ├─ Suppliers/                CRUD
│  ├─ ElectronicsInventory/     Index, AddStock, Edit, Delete
│  ├─ ElectricalInventory/      (same structure)
│  ├─ ToolsInventory/           (same structure)
│  ├─ ModulesInventory/         (same structure)
│  ├─ Usage/                    Report, History, HistoryEdit, HistoryDelete
│  ├─ Damage/                   Report, History, HistoryEdit, HistoryDelete
│  └─ Shared/                   Layouts, partials
└─ wwwroot/                     CSS, JS, Bootstrap + jQuery libs
```

---

## 7. Core Business Logic

### 7.1 UniqueCode Generation
On Master Create: the category maps to a prefix (`E-`, `EL-`, `T-`, `M-`), the
Master sheet is scanned for the highest numeric suffix of that prefix, and the
next code is `prefix + (max + 1)` zero-padded to 3 digits.

### 7.2 Add Stock (Always Append)
Every submission appends a **new row**. Multiple rows per UniqueCode are expected
and represent separate purchase batches. No upsert, no overwrite.

### 7.3 Cost Calculation
`TotalCost = Math.Round(TotalQuantity × CostPerUnit, 2)`. Tax-inclusive. Computed
server-side, never accepted from a form.

### 7.4 Usage / Damage Deduction
1. User selects a specific batch row from the dropdown.
2. Server validates quantity ≤ that batch's `Remaining`.
3. Snapshot the row → decrement `Remaining` → write.
4. Append to `Used_Components` / `Damaged_Components`.
5. **If append fails → restore snapshot (compensating rollback).**

### 7.5 History Edit / Delete (Stock Reversal)
Edit: add old quantity back → validate new quantity → deduct new quantity.
Delete: add quantity back → remove log row.
Both target the original batch by matching on UniqueCode + BatchPurchaseDate.

### 7.6 Low-Stock Detection
Threshold is per-component from Master (`MinStockAlert`, default 5). Total
remaining across **all batches** of a code is compared against the threshold.
Every row of a low component is highlighted.

### 7.7 Sorting
- Master, category inventories: **UniqueCode ascending** (natural order).
- Category inventories secondarily: DateOfPurchase ascending (oldest batch first).
- Usage/Damage history: UniqueCode ascending, then date descending.
- Suppliers: alphabetical by name.

---

## 8. Authentication & Access Control

1. User visits any page → redirected to Google consent screen.
2. Google authenticates → email extracted from the ID token.
3. Email checked against the `AllowedUsers` tab (cached 2 minutes, fail-closed).
4. **Listed** → persistent cookie issued, user proceeds.
5. **Not listed** → Access Denied page, session cleared.

The app refuses to start without OAuth credentials configured, ensuring it never
serves data unprotected.

---

## 9. Deployment — Google Cloud Run

### Prerequisites
1. The spreadsheet has all 9 tabs with exact names and headers.
2. Spreadsheet shared as **Editor** with
   `<project-number>-compute@developer.gserviceaccount.com`.
3. An OAuth 2.0 **Web application** client with redirect URI
   `<service-url>/signin-google`.

### Deploy Command

```bash
gcloud run deploy inventory-ms --source . --region us-central1 \
  --allow-unauthenticated \
  --set-env-vars SpreadsheetId=<id> \
  --set-env-vars GoogleClientId=<client-id> \
  --set-env-vars GoogleClientSecret=<client-secret>
```

`--allow-unauthenticated` is correct: Cloud Run admits the request; the
application enforces sign-in against `AllowedUsers`. Consider
`--set-secrets` (Secret Manager) for the client secret.

---

## 10. Local Development

```powershell
cd Inventory_MS\Inventory_MS

# Set secrets (one time)
dotnet user-secrets set SpreadsheetId "<spreadsheet-id>"
dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>"

# Set credentials
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\path\to\service-account-key.json"

# Build and run
dotnet build    # expect 0 errors, 2 warnings
dotnet run
```

The app listens on `http://localhost:8080` (or the port set via the `PORT` env
var). Add `https://localhost:<port>/signin-google` to the OAuth client's
redirect URIs.

---

## 11. Configuration Reference

| Variable | Source | Required |
|---|---|---|
| `SpreadsheetId` | env / appsettings / user-secrets | Yes |
| `Authentication:Google:ClientId` (or `GoogleClientId`) | env / config | Yes |
| `Authentication:Google:ClientSecret` (or `GoogleClientSecret`) | env / config | Yes |
| `GOOGLE_APPLICATION_CREDENTIALS` | env | No (ADC fallback) |
| `PORT` | env (Cloud Run) | No (default 8080) |

---

## 12. Version History

| Version | Date | Highlights |
|---|---|---|
| **2.3** | 2026-08-20 | Branding update for I2ST Technologies Pvt. Ltd. |
| **2.2** | 2026-08-18 | Google OAuth, multi-batch stock, CSV/ZIP reports, Settings page with dark/light theme, low-stock row highlighting, UniqueCode sorting, navbar redesign |
| **2.0** | 2026-08-12 | Complete rewrite: 8-tab schema, UniqueCode system, Master/Suppliers CRUD, Usage/Damage with rollback, generic InventoryItem model, .NET 10, Cloud Run |
| **1.0** | 2026-08-08 | Initial build: PCB/Panel/Tools, Sl.No. rows, GST columns, App Engine |

---

<p align="center">
  <strong>Inventory Management System</strong><br/>
  I2ST Technologies Pvt. Ltd. &copy; 2026
</p>
