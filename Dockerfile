# Multi-stage build for Squirrel.Wiki
# See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY ["Squirrel.Wiki.sln", "."]

# Copy all project files for restore
COPY ["Squirrel.Wiki.Contracts/Squirrel.Wiki.Contracts.csproj", "Squirrel.Wiki.Contracts/"]
COPY ["Squirrel.Wiki.Core/Squirrel.Wiki.Core.csproj", "Squirrel.Wiki.Core/"]
COPY ["Squirrel.Wiki.Plugins/Squirrel.Wiki.Plugins.csproj", "Squirrel.Wiki.Plugins/"]
COPY ["Squirrel.Wiki.Web/Squirrel.Wiki.Web.csproj", "Squirrel.Wiki.Web/"]
COPY ["plugins/Squirrel.Wiki.Plugins.Oidc/Squirrel.Wiki.Plugins.Oidc.csproj", "plugins/Squirrel.Wiki.Plugins.Oidc/"]
COPY ["plugins/Squirrel.Wiki.Plugins.Lucene/Squirrel.Wiki.Plugins.Lucene.csproj", "plugins/Squirrel.Wiki.Plugins.Lucene/"]
COPY ["plugins/Squirrel.Wiki.Plugins.TableOfContents/Squirrel.Wiki.Plugins.TableOfContents.csproj", "plugins/Squirrel.Wiki.Plugins.TableOfContents/"]

# Restore dependencies
RUN dotnet restore "Squirrel.Wiki.sln"

# Copy all source files
COPY . .

# Build each plugin project individually to ensure proper output structure
WORKDIR "/src/plugins/Squirrel.Wiki.Plugins.Oidc"
RUN dotnet build "Squirrel.Wiki.Plugins.Oidc.csproj" -c Release

WORKDIR "/src/plugins/Squirrel.Wiki.Plugins.Lucene"
RUN dotnet build "Squirrel.Wiki.Plugins.Lucene.csproj" -c Release

WORKDIR "/src/plugins/Squirrel.Wiki.Plugins.TableOfContents"
RUN dotnet build "Squirrel.Wiki.Plugins.TableOfContents.csproj" -c Release

# Build the main projects
WORKDIR "/src"
RUN dotnet build "Squirrel.Wiki.Contracts/Squirrel.Wiki.Contracts.csproj" -c Release
RUN dotnet build "Squirrel.Wiki.Plugins/Squirrel.Wiki.Plugins.csproj" -c Release
RUN dotnet build "Squirrel.Wiki.Core/Squirrel.Wiki.Core.csproj" -c Release

FROM build AS publish
WORKDIR "/src"
RUN dotnet publish "Squirrel.Wiki.Web/Squirrel.Wiki.Web.csproj" -c Release -o /app/publish

# Copy plugins to publish output
# The build targets copy plugins to bin/Release/net8.0/Plugins, so we need to copy them to the publish output
RUN mkdir -p /app/publish/Plugins && \
    cp -r /src/Squirrel.Wiki.Web/bin/Release/net8.0/Plugins/* /app/publish/Plugins/ 2>/dev/null || true

FROM base AS final
WORKDIR /app

# Update base image packages for security
RUN apt-get update && \
    apt-get upgrade -y && \
    rm -rf /var/lib/apt/lists/* && \
    apt-get clean

# Copy published application
COPY --from=publish /app/publish .

# Set the entry point
ENTRYPOINT ["dotnet", "Squirrel.Wiki.Web.dll"]
