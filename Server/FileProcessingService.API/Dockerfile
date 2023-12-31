#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Server/FileProcessingService.API/FileProcessingService.API.csproj", "Server/FileProcessingService.API/"]
COPY ["Server/FileProcessingService.Persistence/FileProcessingService.Persistence.csproj", "Server/FileProcessingService.Persistence/"]
COPY ["Server/FileProcessingService.Application/FileProcessingService.Application.csproj", "Server/FileProcessingService.Application/"]
COPY ["Server/FileProcessingService.Domain/FileProcessingService.Domain.csproj", "Server/FileProcessingService.Domain/"]
COPY ["Server/FileProcessingService.Infrastructure/FileProcessingService.Infrastructure.csproj", "Server/FileProcessingService.Infrastructure/"]
COPY ["Shared/FileProcessingService.Shared/FileProcessingService.Shared.csproj", "Shared/FileProcessingService.Shared/"]
RUN dotnet restore "Server/FileProcessingService.API/FileProcessingService.API.csproj"
COPY . .
WORKDIR "/src/Server/FileProcessingService.API"
RUN dotnet build "FileProcessingService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FileProcessingService.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FileProcessingService.API.dll"]
