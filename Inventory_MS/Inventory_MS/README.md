# Inventory Management System

ASP.NET Core (Razor Pages, .NET 10) inventory management app backed by Google
Sheets (Sheets API v4, service-account auth), deployed to Google Cloud Run.
Sign-in is Google OAuth, restricted to an allow-list held in the spreadsheet.

## 1. Spreadsheet setup

Create a Google Spreadsheet with exactly these tabs (header row in row 1):

- `Master` — Unique Code | Component Name | Category | Brand | Description |
  Unit | Min. Stock Alert
- `Suppliers` — Supplier Name | Contact Info
- `Electronics_Inventory` — Unique Code | Component Name | Brand | Total
  Quantity | Remaining | Invoice No. | Cost per Unit (₹) | Total Cost (₹) |
  Supplier | Date of Purchase | Remarks
- `Electrical_Inventory` — same columns as `Electronics_Inventory`
- `Tools_Inventory` — same columns as `Electronics_Inventory`
- `Modules_Inventory` — same columns as `Electronics_Inventory`
- `Used_Components` — Unique Code | Component Name | Category | Batch
  Purchase Date | Used Date | Quantity Used | Remarks
- `Damaged_Components` — Unique Code | Component Name | Category | Batch
  Purchase Date | Damage Date | Quantity Damaged | Invoice No. |
  Cost per Unit (₹) | Remarks
- `AllowedUsers` — Email  ← **added in v2.2**

Rows are append-only; the app sorts in memory when displaying. Costs are
tax-inclusive: Total Cost = Total Quantity × Cost per Unit.

**Batches (v2.2):** each Add Stock submission appends a new row, so a category
tab holds one row per purchase batch and several rows may share a Unique Code.
Usage and damage are reported against a specific batch.

**Access control:** put one email address per row in `AllowedUsers`. Only those
Google accounts can sign in; everyone else is shown an Access Denied page and
signed out. An empty or unreadable tab denies everyone — it fails closed.

### Service account

Create a service account (Google Cloud Console → IAM & Admin → Service
Accounts), generate a JSON key, and share the spreadsheet with the
service-account email address as **Editor**. Sharing the spreadsheet covers all
tabs including `AllowedUsers`; no separate grant is needed.

On Cloud Run the app uses Application Default Credentials, which is the Compute
Engine default service account
(`<project-number>-compute@developer.gserviceaccount.com`) — that is the address
the spreadsheet must be shared with.

Copy the spreadsheet ID from the URL
(`https://docs.google.com/spreadsheets/d/<ID>/edit`).

## 2. Google OAuth client (v2.2)

Separate from the service account. In Google Cloud Console → APIs & Services →
Credentials, create an **OAuth 2.0 Client ID** of type *Web application* and add
an authorised redirect URI of `<base-url>/signin-google` for every host the app
runs on, for example:

```
https://localhost:62251/signin-google
https://inventory-ms-xxxxxxxx-uc.a.run.app/signin-google
```

Configure the consent screen with the `email` and `profile` scopes.

## 3. Local development

```powershell
dotnet user-secrets set SpreadsheetId "<spreadsheet-id>"
dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>"
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\path\to\service-account-key.json"
dotnet run
```

(`GOOGLE_APPLICATION_CREDENTIALS` can also be a persistent Windows environment
variable. If it is unset and no key file exists, the app falls back to
Application Default Credentials, e.g. from `gcloud auth application-default
login`.)

The app refuses to start when `SpreadsheetId` or the OAuth client credentials are
missing, rather than serving the inventory unprotected.

## 4. Deploy to Cloud Run

```powershell
gcloud run deploy inventory-ms --source . --region us-central1 `
  --allow-unauthenticated `
  --set-env-vars SpreadsheetId=<spreadsheet-id> `
  --set-env-vars GoogleClientId=<client-id> `
  --set-env-vars GoogleClientSecret=<client-secret>
```

`--allow-unauthenticated` is still correct: Cloud Run lets the request through
and the application itself enforces sign-in against `AllowedUsers`. Consider
`--set-secrets` (Secret Manager) instead of `--set-env-vars` for the client
secret.

`Program.cs` contains two Cloud Run specifics that must not be changed: it binds
to the `PORT` environment variable, and `UseHttpsRedirection()` stays commented
out because TLS terminates at Google's load balancer. Forwarded headers are
trusted so the OAuth redirect URI is generated as `https`.

## 5. Notes

- No audit trail: edits overwrite cells without recording who changed what.
- Every read fetches `A1:Z1000` from a tab, so roughly 999 data rows per tab is
  the working ceiling, and there is no caching.
- Reads are not transactional. Two people reporting usage of the same batch at
  the same moment can overwrite each other's deduction.
