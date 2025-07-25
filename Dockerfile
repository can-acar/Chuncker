FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy csproj and restore dependencies
COPY Chuncker/*.csproj ./Chuncker/
COPY Chuncker.Tests/*.csproj ./Chuncker.Tests/
COPY *.sln .
RUN dotnet restore

# Copy all files and build
COPY . .
RUN dotnet build Chuncker/Chuncker.csproj -c Release --no-restore

# Publish
RUN dotnet publish Chuncker/Chuncker.csproj -c Release -o /app --no-build

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

# Create directories for storage
RUN mkdir -p /app/storage
RUN mkdir -p /var/log/chuncker

# Set the entry point
ENTRYPOINT ["dotnet", "Chuncker.dll"]

# Default command (shows help)
CMD ["--help"]
