# Facial Treatment Website — Setup Guide

This guide explains how to get the project from GitHub and run it on a new Windows computer.

## 1. Requirements

Install these before continuing:

- **Git for Windows** — needed to clone the repository.
- **.NET 8 SDK** — the project targets `net8.0`. Verify with `dotnet --list-sdks`.
- **SQL Server Express LocalDB** — install SQL Server Express and include the **LocalDB** feature. Verify with `sqllocaldb info`.
- **SQL Server Management Studio (SSMS)** — optional, but useful for creating or inspecting the local database.

After installing software, close and reopen PowerShell so it refreshes `PATH`.

## 2. Download the source

```powershell
git clone https://github.com/kengloons-oss/facial-treatment-project.git
cd .\facial-treatment-project
```

The web application project is in the `Assignment` subfolder.

## 3. Prepare the local database

The application uses SQL Server LocalDB and attaches `Assignment\DB.mdf` when it starts. Database files are intentionally excluded from GitHub because they contain changeable local data and should not be published with source code.

### Option A — use a supplied database

If the project owner provided a database backup, copy both `DB.mdf` and its matching `DB_log.ldf` into the `Assignment` folder. Do not use a database file from a different version of the project.

### Option B — create a fresh database schema

1. Start LocalDB:

   ```powershell
   sqllocaldb start MSSQLLocalDB
   ```

2. In SSMS, connect to `(localdb)\MSSQLLocalDB` using **Windows Authentication**. Open **New Query** and run the following after replacing the example path with the full path to this clone's `Assignment` folder:

   ```sql
   CREATE DATABASE [DB]
   ON PRIMARY
   (
       NAME = N'DB',
       FILENAME = N'C:\path\to\facial-treatment-project\Assignment\DB.mdf'
   )
   LOG ON
   (
       NAME = N'DB_log',
       FILENAME = N'C:\path\to\facial-treatment-project\Assignment\DB_log.ldf'
   );
   GO
   ```

3. In PowerShell, install the Entity Framework command-line tool if it is not already installed, then apply the project's migrations:

   ```powershell
   dotnet tool install --global dotnet-ef --version 9.0.0
   cd .\Assignment
   dotnet restore
   dotnet ef database update
   cd ..
   ```

   If `dotnet-ef` is already installed, skip the install command. If the database already has the schema, do not create it again; just use the supplied database.

A newly migrated database has the schema but may not contain demo users or sample business data. The sample credentials in `README.txt` only work with the matching demo database. Email verification also needs valid SMTP settings (see below).

## 4. Optional service settings

The site can start in Development without configuring Stripe, Google reCAPTCHA, or SMTP. In Development, reCAPTCHA is disabled by `appsettings.Development.json`. Payment and email features need credentials from your own test accounts.

Set secrets locally with .NET User Secrets; do not put real keys in `appsettings.json` or commit them to Git:

```powershell
cd .\Assignment
# This project already has a UserSecretsId. If using a copy that does not, run:
# dotnet user-secrets init
dotnet user-secrets set "Stripe:SecretKey" "YOUR_STRIPE_TEST_SECRET_KEY"
dotnet user-secrets set "Stripe:PublishableKey" "YOUR_STRIPE_TEST_PUBLISHABLE_KEY"
dotnet user-secrets set "Smtp:User" "YOUR_SMTP_EMAIL"
dotnet user-secrets set "Smtp:Pass" "YOUR_SMTP_APP_PASSWORD"
dotnet user-secrets set "Smtp:Name" "OwO Meow Beauty"
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Smtp:Port" "587"
```

For reCAPTCHA testing, also set your own `ReCaptchaSettings:SiteKey` and `ReCaptchaSettings:SecretKey` values. Do not enable it without valid keys. User Secrets apply to the local Development environment only.

## 5. Start the website

From the cloned repository's top folder, double-click **`Start-Website.bat`**. It starts LocalDB and runs the HTTP Development profile. Keep the command window open while using the website.

Or start it manually in PowerShell:

```powershell
sqllocaldb start MSSQLLocalDB
cd .\Assignment
dotnet run --launch-profile http
```

When the console says it is listening at `http://localhost:5043`, open [http://localhost:5043](http://localhost:5043) in a browser. Press **Ctrl+C** in the console to stop the application.

## 6. Troubleshooting

- **`dotnet` is not recognized**: install the .NET 8 SDK, reopen PowerShell, then check `dotnet --list-sdks`.
- **`sqllocaldb` is not recognized**: rerun SQL Server Express setup and make sure the LocalDB feature is installed. Reopen PowerShell afterward.
- **Cannot attach/open `DB.mdf`**: check that the MDF and matching LDF are in `Assignment`, LocalDB is started, and no other SQL Server process has attached the files. Close SSMS connections before retrying.
- **Port 5043 is already in use**: the website may already be running. Use the existing browser session or stop the other local app first.
- **Sign-in or verification does not work with a new database**: a fresh migration does not include the demo data. Obtain the matching demo database privately, or configure SMTP and register an account.
- **Stripe/email operations fail**: add valid test credentials using the User Secrets commands above. Never paste production credentials into source control.
