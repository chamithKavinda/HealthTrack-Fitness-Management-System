# HealthTrack - Application Development Group Project

## Overview
HealthTrack is a web-based fitness and health tracking management system built with **ASP.NET Core (.NET 10.0)** and **Entity Framework Core**.

This repository contains the initial project scaffold and folder structure established for team collaboration.

---

## Folder Architecture

```text
Project/
├── .gitignore                      # Git ignore rules for .NET and IDEs
├── README.md                       # Project documentation
├── Database/                       # Database scripts and schema
│   ├── .gitkeep
│   └── Downlaod/                   # SQL table scripts
│       └── .gitkeep
└── HealthTrack/                    # ASP.NET Core MVC Web Application
    ├── HealthTrack.sln             # Visual Studio Solution file
    ├── HealthTrack.slnx            # Modern Solution file
    ├── HealthTrack.csproj          # Project configuration & dependencies
    ├── Program.cs                  # Application entry point & service registrations
    ├── appsettings.json            # Configuration & Connection strings
    ├── appsettings.Development.json
    ├── Properties/                 # Launch and runtime settings
    │   ├── launchSettings.json
    │   └── serviceDependencies.json
    ├── Areas/                      # Modular functional areas
    │   └── Identity/Pages/         # Authentication and identity pages
    ├── Controllers/                # MVC Controllers (Account, Admin, Client, Trainer, etc.)
    ├── Models/                     # View models, domain entities, and data models
    ├── Views/                      # Razor Views
    │   ├── Account/                # Authentication views
    │   ├── Admin/                  # Administrator views & dashboards
    │   ├── Client/                 # Client module views
    │   ├── Home/                   # Landing and general views
    │   ├── Notifications/          # User notification views
    │   ├── Shared/                 # Layouts and shared components
    │   └── Trainer/                # Trainer module views
    ├── Data/                       # EF Core DbContexts and migrations
    │   └── Migrations/
    ├── Services/                   # Business logic and domain services
    ├── Security/                   # Security utilities and configuration
    ├── ViewComponent/              # Reusable UI view components
    └── wwwroot/                    # Static assets
        ├── css/                    # Stylesheets
        ├── js/                     # JavaScript files
        └── lib/                    # Vendor libraries (Bootstrap, jQuery)
```

---

## Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Visual Studio 2022 / 2025](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with C# Dev Kit
- [SQL Server](https://www.microsoft.com/sql-server) (LocalDB / Express / Developer)

### Steps to Run
1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd Project
   ```
2. Restore NuGet dependencies:
   ```bash
   dotnet restore HealthTrack/HealthTrack.csproj
   ```
3. Run the application:
   ```bash
   dotnet run --project HealthTrack/HealthTrack.csproj
   ```
4. Access the web application at `https://localhost:7132` or `http://localhost:5222`.

