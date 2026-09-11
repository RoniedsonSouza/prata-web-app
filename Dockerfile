# API Prata — imagem de producao (E1 §3.7)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Prata.Domain/Prata.Domain.csproj src/Prata.Domain/
COPY src/Prata.Application/Prata.Application.csproj src/Prata.Application/
COPY src/Prata.Infrastructure/Prata.Infrastructure.csproj src/Prata.Infrastructure/
COPY src/Prata.Api/Prata.Api.csproj src/Prata.Api/
RUN dotnet restore src/Prata.Api/Prata.Api.csproj
COPY src/ ./src/
RUN dotnet publish src/Prata.Api/Prata.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Prata.Api.dll"]
