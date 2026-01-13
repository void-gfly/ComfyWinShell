# GEMINI.md

This file serves as the primary context and instructional guide for the Gemini agent when working with the **ComfyShell** project.

## 1. Project Overview

**ComfyShell** is a WPF (Windows Presentation Foundation) desktop application built with **.NET 10.0**. Its primary purpose is to manage, configure, and launch **ComfyUI** instances.

**Key Features:**
*   **Profile Management:** Create and manage multiple startup configurations (profiles) for ComfyUI.
*   **Process Monitoring:** Launch, stop, and monitor the status of the ComfyUI process.
*   **Version Management:** Manage different versions of ComfyUI.
*   **Hardware Monitoring:** Monitor system resources (via `LibreHardwareMonitorLib`).

## 2. Architecture & Technology Stack

The project follows a strict **MVVM (Model-View-ViewModel)** architectural pattern.

*   **Framework:** .NET 10.0 (Windows)
*   **UI Framework:** WPF
*   **MVVM Toolkit:** `CommunityToolkit.Mvvm` (v8.4.0)
*   **Dependency Injection:** `Microsoft.Extensions.DependencyInjection` & `Microsoft.Extensions.Hosting`
*   **Configuration:** `Microsoft.Extensions.Configuration` (JSON based)
*   **Logging:** `Microsoft.Extensions.Logging`

### Directory Structure

*   `Models/`: Data objects and Enums.
    *   `ComfyConfiguration.cs`: Represents launch arguments for ComfyUI.
    *   `Profile.cs`: Represents a user profile containing a configuration.
    *   `Enums/`: Defines strongly-typed options for ComfyUI args (e.g., `VramMode`, `AttentionMode`).
*   `ViewModels/`: Logic for the views, inheriting from `ViewModelBase`.
    *   Uses `[ObservableProperty]` for automatic property generation.
    *   Uses `[RelayCommand]` for command generation.
*   `Views/`: XAML files defining the UI layout.
*   `Services/`: Encapsulates business logic.
    *   `ProcessService.cs`: Handles ComfyUI process execution.
    *   `ConfigurationService.cs` & `ProfileService.cs`: Manages persistence of settings and profiles.
    *   `ArgumentBuilder.cs`: Converts `ComfyConfiguration` objects into command-line strings.
*   `App.xaml.cs`: Application entry point, responsible for DI container configuration and global exception handling.

## 3. Building and Running

**Prerequisites:** .NET 10.0 SDK.

**Commands:**

*   **Build:**
    ```powershell
    dotnet build
    ```

*   **Run:**
    ```powershell
    dotnet run
    ```
    *Note: Ensure `appsettings.json` is present in the output directory (handled by `.csproj` copy task).*

*   **Publish (Release):**
    ```powershell
    dotnet publish -c Release
    ```

## 4. Development Conventions

### Coding Style
*   **Namespaces:** Use file-scoped namespaces (e.g., `namespace WpfDesktop.ViewModels;`).
*   **Classes:** PascalCase. ViewModels should end with `ViewModel`, Services with `Service`.
*   **Interfaces:** PascalCase with `I` prefix (e.g., `IProcessService`).
*   **Async/Await:** Always use `async Task` instead of `async void` (except for top-level event handlers).
*   **Nullability:** The project enables nullable reference types (`<Nullable>enable</Nullable>`). Handle nulls explicitly.

### MVVM Patterns
*   Inherit ViewModels from `ViewModelBase` (`CommunityToolkit.Mvvm.ComponentModel.ObservableObject`).
*   Use `[ObservableProperty]` on `private` backing fields (camelCase with underscore, e.g., `_isLoading`) to generate `public` properties (PascalCase, e.g., `IsLoading`).
*   Use `[RelayCommand]` on methods to expose them as `ICommand`s to the View.

### Service Registration
*   Register all new services in `App.xaml.cs` within the `CreateHostBuilder` method.
*   Prefer Singleton lifetime for stateful services (like configuration or process managers).

### Configuration & Data
*   **User Profiles:** Stored as JSON files in `%LOCALAPPDATA%\ComfyShell\profiles\`.
*   **App Settings:** Stored in `appsettings.json` (e.g., global language or theme settings).
*   **File Encoding:** All files must be UTF-8.

## 5. Key Files

*   `WpfDesktop.csproj`: Project definition and NuGet dependencies.
*   `App.xaml.cs`: DI setup and startup logic.
*   `MainWindow.xaml`: Main application shell.
*   `MainViewModel.cs`: Top-level ViewModel coordinating navigation and application state.
*   `Services/ProcessService.cs`: Core logic for launching ComfyUI.
*   `Services/ArgumentBuilder.cs`: Logic mapping UI options to CLI arguments.

## 6. Testing
*   Currently, there are no dedicated test projects in the solution.
*   Future tests should follow the convention `WpfDesktop.Tests` and use `xUnit` or `NUnit`.
