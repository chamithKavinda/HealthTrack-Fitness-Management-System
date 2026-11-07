# 🏋️ HealthTrack - Fitness & Health Management System

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-MVC-512BD4?style=flat&logo=aspnet)](https://dotnet.microsoft.com/apps/aspnet)
[![Entity Framework Core](https://img.shields.io/badge/Entity_Framework_Core-10.0-512BD4?style=flat)](https://learn.microsoft.com/ef/core/)
[![Database](https://img.shields.io/badge/Database-SQL_Server-CC292B?style=flat&logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![License](https://img.shields.io/badge/License-Academic_Use-blue?style=flat)](#license)

A modern, enterprise-grade web-based fitness and health tracking management system developed with **ASP.NET Core (.NET 10.0) MVC**, **Entity Framework Core**, and **Microsoft SQL Server**.

HealthTrack provides an end-to-end portal catering to three distinct roles: **Clients**, **Trainers**, and **System Administrators**. It enables workout and meal logging, interactive fitness progress tracking, appointment scheduling, personalized diet/workout plan creation, automated user notifications, and administrative reporting.

---

## 📑 Table of Contents

- [Key Features](#-key-features)
  - [Client Portal](#1-client-portal)
  - [Trainer Portal](#2-trainer-portal)
  - [Admin Portal](#3-admin-portal)
  - [System-wide Capabilities](#4-system-wide-capabilities)
- [Architecture & Tech Stack](#-architecture--tech-stack)
- [Project Directory Structure](#-project-directory-structure)
- [Prerequisites](#-prerequisites)
- [Installation & Setup Guide](#-installation--setup-guide)
  - [Step 1: Clone the Repository](#step-1-clone-the-repository)
  - [Step 2: Database Configuration](#step-2-database-configuration)
  - [Step 3: Restore Dependencies & Build](#step-3-restore-dependencies--build)
  - [Step 4: Run the Application](#step-4-run-the-application)
- [Running in Visual Studio / Visual Studio Code](#-running-in-visual-studio--visual-studio-code)
  - [Visual Studio (Windows / Mac)](#visual-studio-2022--2025)
  - [Visual Studio Code (Cross-Platform)](#visual-studio-code)
- [Team Collaboration & Branching Strategy](#-team-collaboration--branching-strategy)
- [Troubleshooting & Common Issues](#-troubleshooting--common-issues)
- [Team Contributors](#-team-contributors)

---

## 🌟 Key Features

### 1. Client Portal
- **Interactive Dashboard**: Real-time fitness overview, daily caloric intake, logged routines, and upcoming bookings.
- **Workout Logging**: Track workout routines, exercise sets, reps, weight, and cardio duration.
- **Meal & Nutrition Tracker**: Log daily meals (Breakfast, Lunch, Dinner, Snacks) with calories, protein, carbs, and fats.
- **Progress Tracking & Analytics**: Interactive chart visualizations for body weight, BMI trends, and fitness goals over time.
- **Diet & Workout Plans**: View assigned customized workout regimes and dietary plans tailored by personal trainers.
- **Appointment Booking**: Schedule, review, or reschedule training sessions with assigned trainers.
- **Client Feedback**: Provide feedback on sessions, diet plans, and trainers.
- **In-App Notification Center**: Instant bell alerts and central notification hub for appointment updates, plan revisions, and reminders.

### 2. Trainer Portal
- **Trainer Dashboard**: Monitor assigned clients, schedule, and daily training milestones.
- **Client Roster Management**: Inspect client profiles, health metrics, workout histories, and nutritional logs.
- **Workout & Diet Plan Builder**: Formulate customized workout splits and meal plans for specific client goals.
- **Appointment Operations**: Accept, update, or cancel training sessions with real-time schedule conflict validation.
- **Client Progress Reviews**: Track client improvements and write feedback notes.

### 3. Admin Portal
- **Executive Dashboard**: System KPIs, total active users, trainer-client assignment rates, and activity overviews.
- **User Management**: Comprehensive CRUD operations, user search, account lock/unlock, and password resets.
- **Trainer-Client Assignments**: Manage workload distribution and pair clients with dedicated trainers.
- **System Reports & Exports**: Generate system audit logs, user statistics, and operational reports with CSV export support.

### 4. System-wide Capabilities
- **Role-Based Access Control (RBAC)**: Fine-grained security separating `Admin`, `Trainer`, and `Client` permissions using ASP.NET Core Identity.
- **Security & Data Protection**: Anti-forgery validation (`[AutoValidateAntiforgeryToken]`), cookie security, and field-level encryption for sensitive metrics.
- **Theme Support**: Seamless Light/Dark mode with automatic system preference detection and localStorage persistence.
- **Responsive UI**: Built with modern CSS design tokens and Bootstrap 5 for smartphones, tablets, and desktop displays.

---

## 🛠️ Architecture & Tech Stack

| Layer | Technologies / Libraries |
|---|---|
| **Framework** | ASP.NET Core (.NET 10.0 Web App / MVC) |
| **Language** | C# 13 / .NET 10 |
| **Authentication & Auth** | ASP.NET Core Identity, Cookie Authentication, Identity Roles |
| **ORM & Data Access** | Entity Framework Core 10.0 (Code-First / Migrations) |
| **Database** | Microsoft SQL Server (LocalDB / Express / Azure SQL) |
| **Frontend / UI** | Razor Views (`.cshtml`), Bootstrap 5, Bootstrap Icons |
| **Client Scripting** | JavaScript (ES6+), Chart.js / Canvas-based interactive graphs |
| **Data Protection** | ASP.NET Core Data Protection, Custom AES field encryption |

---

## 📂 Project Directory Structure

```text
HealthTrack-Fitness-Management-System/
├── .gitignore                          # Rules to ignore build artifacts, logs, and IDE caches
├── README.md                           # Master project documentation and setup manual
├── Database/                           # Database documentation and SQL scripts
│   ├── .gitkeep
│   ├── HealthTrackDB.sql               # Full database schema & seed scripts (if applicable)
│   └── Downlaod/                       # Supplementary SQL query files
│       └── .gitkeep
└── HealthTrack/                        # Core ASP.NET Core Solution & Project
    ├── HealthTrack.sln                 # Visual Studio Solution File
    ├── HealthTrack.slnx                # Modern Visual Studio XML Solution File
    ├── HealthTrack.csproj              # Project dependencies and SDK configurations
    ├── Program.cs                      # Dependency Injection container & HTTP middleware pipeline
    ├── appsettings.json                # Application configuration & connection strings
    ├── appsettings.Development.json    # Development environment settings
    ├── Properties/
    │   └── launchSettings.json         # Development server ports (HTTP: 5222, HTTPS: 7132)
    ├── Areas/
    │   └── Identity/Pages/             # Identity UI override pages (Login, Register, Logout)
    ├── Controllers/                    # MVC Controllers
    │   ├── AccountController.cs        # User authentication & registration
    │   ├── AdminController.cs          # User administration, assignments, system reports
    │   ├── ClientController.cs         # Workouts, meals, progress, plans, appointments
    │   ├── HomeController.cs           # Public landing, privacy, error handling
    │   ├── NotificationsController.cs  # Notification management API and view actions
    │   └── TrainerController.cs        # Client rosters, plan drafting, feedback
    ├── Models/                         # Domain entities & ViewModels
    │   ├── AccountViewModels.cs        # Login, Register, Forgot Password models
    │   ├── AdminModels.cs              # User administration & assignment DTOs
    │   ├── AppointmentModels.cs        # Training session models
    │   ├── ClientModuleModels.cs       # Workout, meal, and health metric models
    │   ├── DashboardViewModels.cs      # Role-specific dashboard aggregations
    │   ├── NotificationModels.cs       # Alert and notification models
    │   └── TrainerModels.cs            # Trainer-specific views and plan specifications
    ├── Data/                           # Persistence layer
    │   ├── ApplicationDbContext.cs     # Identity & Auth DbContext
    │   ├── HealthTrackContext.cs       # Domain entities DbContext
    │   ├── RoleSeeder.cs               # Initial Roles & Default Admin seed helper
    │   └── Migrations/                 # EF Core database migrations
    ├── Services/                       # Application & business services
    │   ├── AppointmentRules.cs         # Scheduling validation logic
    │   ├── CsvWriter.cs                # System report generation utility
    │   ├── FieldEncryptor.cs           # Cryptographic protection for sensitive data
    │   └── HealthMath.cs               # BMI, BMR, and caloric requirement calculations
    ├── Security/                       # Custom security policies & headers
    ├── ViewComponent/                  # Modular Razor ViewComponents (e.g. Notifications bell)
    ├── Views/                          # Razor Views organized by Controller
    │   ├── Account/                    # Login, Register, AccessDenied
    │   ├── Admin/                      # Dashboard, Users, Assignments, Reports
    │   ├── Client/                     # Dashboard, Workouts, Meals, Progress, Plans, Appointments
    │   ├── Home/                       # Index (Landing page), Privacy
    │   ├── Notifications/              # Notifications index & history
    │   ├── Shared/                     # Layouts (_Layout), Navbars (_ClientNav, _TrainerNav, _AdminNav)
    │   │   └── Components/             # ViewComponent templates (Bell, List)
    │   ├── Trainer/                    # Dashboard, Clients, Plans, Appointments
    │   ├── _ViewImports.cshtml         # Global tag helpers and using directives
    │   └── _ViewStart.cshtml           # Default layout specification
    └── wwwroot/                        # Static client-side assets
        ├── css/                        # Custom stylesheets (`site.css` design system)
        ├── js/                         # JavaScript logic (`site.js`, `progress.js`, `reports.js`)
        └── lib/                        # Bootstrap, jQuery, and jQuery Validation libraries
```

---

## 💻 Prerequisites

Ensure you have the following installed on your machine:

1. **.NET 10.0 SDK** (or later)
   - Verify installation:
     ```bash
     dotnet --version
     # Expected output: 10.0.xxx
     ```
   - Download from: [Microsoft .NET 10 Download](https://dotnet.microsoft.com/download/dotnet/10.0)

2. **Microsoft SQL Server**
   - **Windows**: SQL Server Express or LocalDB (comes with Visual Studio)
   - **macOS / Linux**: Azure SQL Edge or SQL Server in Docker:
     ```bash
     docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong@Password123" \
        -p 1433:1433 --name sqlserver --hostname sqlserver -d \
        mcr.microsoft.com/mssql/server:2022-latest
     ```

3. **IDE / Code Editor**:
   - [Visual Studio 2022 / 2025](https://visualstudio.microsoft.com/) (recommended on Windows with *.NET desktop development* and *ASP.NET and web development* workloads)
   - [Visual Studio Code](https://code.visualstudio.com/) with **C# Dev Kit** extension (recommended for macOS / Linux)
   - [JetBrains Rider](https://www.jetbrains.com/rider/) (cross-platform)

4. **Git CLI**:
   - Download from: [git-scm.com](https://git-scm.com/)

---

## 🚀 Installation & Setup Guide

### Step 1: Clone the Repository

Open your terminal or command prompt and clone the repository:

```bash
git clone https://github.com/chamithKavinda/HealthTrack-Fitness-Management-System.git
cd HealthTrack-Fitness-Management-System
```

---

### Step 2: Database Configuration

1. Navigate to the project directory:
   ```bash
   cd HealthTrack
   ```

2. Open [`appsettings.json`](file:///Users/chamithkavinda/Documents/Esoft/Application%20Development/Assignment%202/Project/HealthTrack/appsettings.json) and verify or update the connection string to match your local SQL Server instance:

   **For Windows (SQL Server LocalDB / Windows Authentication):**
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HealthTrackDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
   }
   ```

   **For Windows (SQL Server Express):**
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=.\\SQLEXPRESS;Database=HealthTrackDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
   }
   ```

   **For macOS / Linux (Docker SQL Server / SQL Authentication):**
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost,1433;Database=HealthTrackDB;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;MultipleActiveResultSets=true"
   }
   ```

3. **Apply Database Migrations / Initialize Schema**:
   Run Entity Framework Core migrations to generate the database schema and default tables:
   ```bash
   dotnet ef database update
   ```

   > 💡 **Note**: If `dotnet-ef` is not installed globally, install it with:
   > ```bash
   > dotnet tool install --global dotnet-ef
   > ```

---

### Step 3: Restore Dependencies & Build

Restore the required NuGet packages and compile the solution:

```bash
# Navigate to the HealthTrack project folder
cd HealthTrack

# Restore NuGet packages
dotnet restore

# Build project in Debug configuration
dotnet build
```

---

### Step 4: Run the Application

Launch the development server:

```bash
dotnet run
```

When started, the terminal will display output similar to:
```text
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7132
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5222
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

Open your web browser and navigate to:
- **HTTPS**: [https://localhost:7132](https://localhost:7132)
- **HTTP**: [http://localhost:5222](http://localhost:5222)

---

## 🖥️ Running in Visual Studio / Visual Studio Code

### Visual Studio (2022 / 2025)
1. Double-click [`HealthTrack/HealthTrack.sln`](file:///Users/chamithkavinda/Documents/Esoft/Application%20Development/Assignment%202/Project/HealthTrack/HealthTrack.sln) to open the solution in Visual Studio.
2. Allow Visual Studio to restore NuGet packages automatically (or right-click Solution > **Restore NuGet Packages**).
3. Ensure the startup project is set to **HealthTrack** (bold text in Solution Explorer).
4. Select the target profile (e.g., `https` or `HealthTrack`) from the run dropdown.
5. Press **`F5`** (Debug) or **`Ctrl + F5`** (Start Without Debugging).
6. Your browser will launch automatically pointing to the local application.

### Visual Studio Code
1. Open the root workspace in VS Code:
   ```bash
   code .
   ```
2. Ensure you have the **C# Dev Kit** extension installed.
3. Open the integrated terminal (`Ctrl + \`` or `Cmd + \``).
4. Run:
   ```bash
   dotnet run --project HealthTrack/HealthTrack.csproj
   ```
5. Click on the generated localhost URL to view the application.

---

## 🤝 Team Collaboration & Branching Strategy

This project is maintained by a 3-member group. To avoid merge conflicts and ensure clean collaboration, please adhere to the following workflow:

### Branching Rules
- **`main`**: Production-ready code. Do not commit directly to `main`.
- **`chamith`**: Feature branch for Client Portal, Progress Analytics & Notifications.
- **`sanjula`**: Feature branch for Authentication, Admin Portal & Reports.
- **`dilshan`**: Feature branch for Trainer Portal & Workout/Diet Plans.

### Collaboration Workflow

1. **Before starting work, update your local branch with `main`:**
   ```bash
   git checkout <your-branch>
   git fetch origin
   git merge origin/main
   ```

2. **Work on your designated module, then stage and commit with clear messages:**
   ```bash
   git add .
   git commit -m "feat: implement appointment booking validation"
   ```

3. **Push changes to your remote branch:**
   ```bash
   git push origin <your-branch>
   ```

4. **Merge into `main`:**
   - Create a Pull Request (PR) on GitHub from `<your-branch>` into `main`.
   - Have at least one team member review the code.
   - Once approved, merge the PR into `main`.

---

## ❓ Troubleshooting & Common Issues

<details>
<summary><strong>1. "nodename nor servname provided, or not known (api.nuget.org:443)"</strong></summary>

- **Cause**: Network connectivity issue or offline environment preventing NuGet from restoring packages.
- **Solution**: Ensure your internet connection is active, or configure a proxy if you are behind an enterprise network:
  ```bash
  dotnet restore --no-cache
  ```
</details>

<details>
<summary><strong>2. Cannot connect to SQL Server / "A network-related or instance-specific error occurred"</strong></summary>

- **Cause**: SQL Server service is not running or the connection string in `appsettings.json` is incorrect.
- **Solution**:
  - **Windows**: Open `services.msc` and verify that `SQL Server (MSSQLSERVER)` or `SQL Server (SQLEXPRESS)` is in **Running** state.
  - **Docker**: Verify container status with `docker ps`. If stopped, run `docker start sqlserver`.
  - Check that `Server=` and credentials match your local SQL Server instance.
</details>

<details>
<summary><strong>3. HTTPS / SSL Certificate Warning in Browser</strong></summary>

- **Cause**: The local ASP.NET Core development certificate is not trusted.
- **Solution**: Run the following command in terminal:
  ```bash
  dotnet dev-certs https --clean
  dotnet dev-certs https --trust
  ```
</details>

<details>
<summary><strong>4. Missing Migrations / "Invalid object name" SQL Exception</strong></summary>

- **Cause**: The database exists but tables have not been created yet.
- **Solution**: Run database updates:
  ```bash
  dotnet ef database update --project HealthTrack/HealthTrack.csproj
  ```
</details>

---

## 👥 Team Contributors

| Name | Role | Responsibilities | Branch |
|---|---|---|---|
| **Chamith Kavinda** | Full Stack Developer | Client Portal, Workout & Meal Logging, Progress Charts, Notifications | `chamith` |
| **Sanjula** | Full Stack Developer | Authentication (Identity), Admin Dashboard, User Management, Reports | `sanjula` |
| **Dilshan** | Full Stack Developer | Trainer Portal, Client Management, Workout & Diet Plans, Scheduling | `dilshan` |

---

## 📄 License
This project is developed as an academic group assignment for the Application Development module. All rights reserved by the development team.
