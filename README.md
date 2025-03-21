# Product Gallery System

A product display system built with ASP.NET Core MVC that fetches product data from external APIs, stores it in a local database, and provides an attractive product browsing interface.

## Features

- Automatic product data synchronization from external APIs
- Data storage in local MS SQL Server database
- Responsive user interface built with Tailwind CSS
- Browse products by category
- View detailed product information and images
- Automatic database creation and migration

## Technology Stack

- **Backend**: ASP.NET Core MVC (.NET 9.0)
- **Database**: Microsoft SQL Server
- **ORM**: Entity Framework Core
- **Frontend Styling**: Tailwind CSS
- **API Communication**: HttpClient

## System Requirements

- .NET 9.0 SDK or higher
- Microsoft SQL Server (local or remote)
- Web browser

## Installation Steps

1. Clone the repository:

   ```
   git clone https://github.com/SarahL4/UCSProuctGallery.git
   cd UCSProductGallery
   ```

2. Update the database connection string:

   - Find the `ConnectionStrings` section in the `appsettings.json` file
   - Modify the `DefaultConnection` value according to your environment

3. Build and run the application:

   ```
   cd ProductGallery/UCSProductGallery
   dotnet build
   dotnet run
   ```

   If you encounter issues, use the full project path:

   ```
   dotnet run --project ProductGallery/UCSProductGallery/UCSProductGallery.csproj
   ```

4. Access the application in your browser:
   ```
   https://localhost:5001
   ```
   or
   ```
   http://localhost:5000
   ```

## Usage Guide

- **Browse Products**: The homepage displays all products; click on a product to view details
- **Filter by Category**: Use the navigation menu to select a specific category
- **Synchronize Products**: Manually trigger product synchronization from the management interface
- **Search Function**: Use the search box to find specific products

## Development

### Project Structure

- `Controllers/`: Contains MVC controllers
- `Models/`: Data model classes
- `Views/`: Razor view files
- `Services/`: Business logic and API communication
- `Data/`: Data access and EF Core configuration

### Running Tests

```
cd ProductGallery.Tests
dotnet test
```

## License

[MIT License](LICENSE)
