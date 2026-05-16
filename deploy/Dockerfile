FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Pos.sln Directory.Build.props ./
COPY src/Pos.BuildingBlocks/ src/Pos.BuildingBlocks/
COPY src/Pos.Domain/ src/Pos.Domain/
COPY src/Pos.Application/ src/Pos.Application/
COPY src/Pos.Infrastructure/ src/Pos.Infrastructure/
COPY src/Pos.Api/ src/Pos.Api/
RUN dotnet restore Pos.sln
RUN dotnet publish src/Pos.Api/Pos.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN groupadd -r app && useradd -r -g app app
COPY --from=build /app .
USER app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pos.Api.dll"]
