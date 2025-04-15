# Use the Azure Functions isolated runtime as the base image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
EXPOSE 8080


# Use the .NET SDK image for building the Azure Function app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AzureCheck/AzureCheck.csproj", "AzureCheck/"]
RUN dotnet restore "./AzureCheck/AzureCheck.csproj"
COPY . .
WORKDIR "/src/AzureCheck"
RUN dotnet build "./AzureCheck.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the app
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AzureCheck.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Add necessary dependencies (Chrome, Selenium, etc.) in the final image
FROM base AS final
WORKDIR /home/site/wwwroot

# Install Chrome and the necessary dependencies for Selenium
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    unzip \
    libxss1 \
    fonts-liberation \
    libnss3 \
    libgdk-pixbuf2.0-0 \
    libx11-xcb1 \
    libdbus-1-3 \
    libxtst6 \
    xdg-utils \
    libasound2 \
    libvulkan1 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libatspi2.0-0 \
    libcairo2 \
    libcups2 \
    libgtk-3-0 \
    libpango-1.0-0 \
    libxdamage1 \
    libxkbcommon0 \
    && rm -rf /var/lib/apt/lists/*

# Download and install Chrome (for Selenium)
RUN wget https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
RUN dpkg -i google-chrome-stable_current_amd64.deb
RUN apt-get install -f -y  # Fix missing dependencies if any

# Copy the published function app from the build stage
COPY --from=publish /app/publish .

# Environment variable setup
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true
   