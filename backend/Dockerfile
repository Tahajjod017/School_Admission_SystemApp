# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Admission.Api/Admission.Api.csproj src/Admission.Api/
RUN dotnet restore src/Admission.Api/Admission.Api.csproj
COPY src/ src/
RUN dotnet publish src/Admission.Api/Admission.Api.csproj -c Release -o /app --no-restore

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Admission.Api.dll"]
