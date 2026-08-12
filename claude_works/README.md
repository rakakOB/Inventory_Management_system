# Smart Signage Board — Inventory Management

ASP.NET Core (Razor Pages) inventory management app backed by Google Sheets
(Sheets API v4, service-account auth), deployed to Google Cloud App Engine
Standard.

## 1. Spreadsheet setup

1. Create a Google Spreadsheet with exactly these tabs (header row in row 1):

   - `PCB_Inventory` — Sl. No. | Category | Component Name | Total Quantity |
     Remaining | Invoice No. | Cost per Unit (₹) | Base Cost (₹) | GST 18% (₹) |
     Total Cost (₹) | Supplier | Date of Purchase | Remarks
   - `Tools_Inventory` — Sl. No. | Tool Name | Category | Total Quantity |
     Available | Invoice No. | Cost per Unit (₹) | Total Cost (₹) | Supplier |
     Date of Purchase | Remarks
   - `Panel_Inventory` — Sl. No. | Category | Component Name | Total Quantity |
     Remaining | Invoice No. | Cost per Unit (₹) | Total Cost (₹) | Supplier |
     Date of Purchase | Remarks
   - `Damages_Components` — Sl. No. | Date | Component Name | Category |
     Quantity Damaged | Reason for Damage | Invoice No. | Cost per Unit (₹) | Remarks

2. Create a service account (Google Cloud Console → IAM & Admin → Service
   Accounts), generate a JSON key, and share the spreadsheet with the
   service-account email address as **Editor**.
3. Copy the spreadsheet ID from the URL (`https://docs.google.com/spreadsheets/d/<ID>/edit`).

## 2. Local development

```powershell
dotnet user-secrets set SpreadsheetId "<spreadsheet-id>"
$env:GOOGLE_APPLICATION_CREDENTIALS = "C:\path\to\service-account-key.json"
dotnet run
```

(`GOOGLE_APPLICATION_CREDENTIALS` can also be set as a persistent Windows
environment variable. If it is unset and no key file exists, the app falls
back to Application Default Credentials, e.g. from `gcloud auth application-default login`.)

## 3. Deploy to App Engine

1. Store the key in Secret Manager:

   ```powershell
   gcloud secrets create SERVICE_ACCOUNT_KEY --data-file=service-account-key.json
   ```

2. Grant the App Engine default service account read access to the secret
   (replace `<project>` with your project id):

   ```powershell
   gcloud secrets add-iam-policy-binding SERVICE_ACCOUNT_KEY `
     --member="serviceAccount:<project>@appspot.gserviceaccount.com" `
     --role="roles/secretmanager.secretAccessor"
   ```

3. In `app.yaml`: set `SpreadsheetId` and make sure
   `GOOGLE_APPLICATION_CREDENTIALS` points to the mounted file (files in a
   secret volume are named after the secret id).

4. Deploy:

   ```powershell
   gcloud app deploy app.yaml
   ```

> **Note:** This quick version has no authentication. If the app is reachable
> from the public internet, restrict access with Identity-Aware Proxy (IAP)
> or add authentication before going live.

> **Note:** `instance_class: B1` (256 MB) is the smallest available class. If
> the app reports memory pressure, switch to `B2` (512 MB).
