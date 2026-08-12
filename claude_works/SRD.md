# System Requirements Document

## General Inventory Management System (GIMS) — v2.0

---

## 1. Overview

A web application for managing electronic/electrical components, tools, and
modules using Google Sheets as the live database. It covers all core inventory
modules: **Product Management, Category Management, Supplier Management,
Inventory/Stock Management, and basic Dashboard & Reporting**. The system is
designed for internal use (no sales), tracking purchases, stock levels, usage,
and damage.

---

## 2. Core Modules

| Module | Description | Implementation |
| --- | --- | --- |
| **Dashboard** | At-a-glance summary of inventory health. | Home page showing total items, low-stock alerts, stock value. |
| **Product Management** | Central catalogue of all items. | Master sheet – unique code, name, category, brand, description. |
| **Category Management** | Grouping items into logical types. | Category is a column in Master (Electronics, Electrical, Tools, Modules). For simplicity, no separate Category sheet. |
| **Supplier Management** | List of approved suppliers. | Suppliers sheet – name, contact (optional). Supplier fields in other sheets use dropdowns validated against this list. |
| **Inventory/Stock Management** | Tracking quantities, locations (category sheets), and movements (in, out, damaged). | Four category inventory sheets + usage/damage deduction logic. |
| **Transaction History** | Audit trail of all stock reductions. | Used_Components and Damaged_Components sheets, with edit/delete and rollback. |

### 2.1 Out of Scope for V2 — Future Roadmap

The following modules are **not part of the V2 build**, but the system will be
structured so they can be added later without rework.

| Module | Description | Planned Implementation |
| --- | --- | --- |
| **User Management** | Eventually, for multi-user access. | Not now — but the structure will be designed so it can be added later (Google Sign-In + allowed-users sheet). |
| **Reports & Analytics** | Basic reports are enough. | Add a simple "low stock report" and "total stock value" on the dashboard. Export to CSV/Excel is a future idea. |
| **Settings** | Only the spreadsheet ID and credentials. | Done via configuration files and Secret Manager. No settings page needed for now. |

> The dashboard already shows total stock value and low-stock alerts
> (see Module: Dashboard), so the basic reporting requirements are covered in
> V2; richer reports and export remain future work.

---

## 3. Google Sheets Schema

All tabs reside in one spreadsheet. Physical rows are **append-only**; all
sorting is performed by the application in memory.

### 3.1 Master — Product Management

| Column | Name | Description |
| --- | --- | --- |
| A | Unique Code | Auto-generated (e.g., E-001, T-012) |
| B | Component Name | Primary sort key |
| C | Category | Electronics / Electrical / Tools & Instruments / Modules |
| D | Brand | (optional) |
| E | Description | (optional) |
| F | Unit | e.g., pcs (default) |
| G | Min Stock Alert | Low-stock warning threshold |

### 3.2 Suppliers — Supplier Management

| Column | Name | Description |
| --- | --- | --- |
| A | Supplier Name | Unique, used for dropdowns |
| B | Contact Info | (optional) |

### 3.3 Category Inventory Sheets

Names: `Electronics_Inventory`, `Electrical_Inventory`, `Tools_Inventory`,
`Modules_Inventory`

| Column | Name | Description |
| --- | --- | --- |
| A | Unique Code | FK to Master |
| B | Component Name | (read-only) |
| C | Brand | (read-only) |
| D | Total Quantity | Purchased |
| E | Remaining | Current stock |
| F | Invoice No. | |
| G | Cost per Unit (₹) | Tax-inclusive |
| H | Total Cost (₹) | = TotalQty × CostPerUnit |
| I | Supplier | Dropdown from Suppliers sheet |
| J | Date of Purchase | yyyy-MM-dd |
| K | Remarks | |

### 3.4 Used_Components — Usage History

| Column | Name |
| --- | --- |
| A | Unique Code (FK) |
| B | Component Name |
| C | Category |
| D | Batch Purchase Date (from source row) |
| E | Used Date |
| F | Quantity Used |
| G | Remarks |

### 3.5 Damaged_Components — Damage History

| Column | Name |
| --- | --- |
| A | Unique Code |
| B | Component Name |
| C | Category |
| D | Batch Purchase Date |
| E | Damage Date |
| F | Quantity Damaged |
| G | Invoice No. (optional) |
| H | Cost per Unit (optional) |
| I | Remarks |

---

## 4. Sorting Strategy

Physical sheets are append-only – new rows always go to the bottom. The
application sorts all data before displaying:

- **Master, Category sheets, Suppliers:** alphabetical by name.
- **Used/Damaged:** by Component Name, then by Used/Damage Date (descending).

---

## 5. Web Application Pages (Navigation)

Navbar: **Home | Electronics | Electrical | Tools & Instruments | Modules |
Report Usage | Usage History | Report Damage | Damage History | Suppliers |
Master List**

- **Home:** Dashboard with counts, low-stock warnings, total stock value.
- **Master List:** CRUD for Master sheet. Add form auto-generates Unique Code
  (prefix + sequential number).
- **Suppliers:** Simple CRUD page for the Suppliers sheet.
- **Category Inventory pages:** Table sorted alphabetically. Add Stock button
  opens form with dropdown of Master items and Supplier dropdown. If component
  already exists by code, update the row; otherwise append new row. Edit/Delete
  on each row.
- **Report Usage / Damage:** Select category → dropdown of items with stock > 0
  → enter quantity, date, remarks → validate → deduct stock → append history
  record with rollback.
- **Usage History / Damage History:** Sorted list with Edit/Delete, and
  automatic stock reversal on edit/delete.

---

## 6. Technical Stack

- ASP.NET Core Razor Pages (.NET 8/10)
- Google Sheets API v4 (service account)
- Bootstrap 5 (corporate UI)
- Deployment: Google App Engine Standard (free tier compatible)
