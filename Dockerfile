# ---- Stage 1: build the app using the .NET SDK image ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first, so package restore can be cached
COPY DocumentManagement.Api/DocumentManagement.Api.csproj DocumentManagement.Api/
RUN dotnet restore DocumentManagement.Api/DocumentManagement.Api.csproj

# Copy the rest of the API code and publish it
COPY DocumentManagement.Api/ DocumentManagement.Api/
RUN dotnet publish DocumentManagement.Api/DocumentManagement.Api.csproj -c Release -o /app/publish --no-restore

# ---- Stage 2: run the app using the smaller runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DocumentManagement.Api.dll"]