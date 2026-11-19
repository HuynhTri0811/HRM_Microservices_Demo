FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8081
EXPOSE 9081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["QuanLyNhanSuMicroservice/QuanLyNhanSuMicroservice.csproj", "QuanLyNhanSuMicroservice/"]
RUN dotnet restore "QuanLyNhanSuMicroservice/QuanLyNhanSuMicroservice.csproj"
COPY . .
WORKDIR "/src/QuanLyNhanSuMicroservice"
RUN dotnet build "QuanLyNhanSuMicroservice.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QuanLyNhanSuMicroservice.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Port do Kestrel:Endpoints trong appsettings.json quyết định: 8081 (REST, HTTP/1.1) và 9081 (gRPC, HTTP/2)
ENTRYPOINT ["dotnet", "QuanLyNhanSuMicroservice.dll"]
