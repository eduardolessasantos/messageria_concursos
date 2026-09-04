# ==============================================================================
# Dockerfile na Raiz do Repositório (Padrão para Render Web Service)
# Constrói e publica o Concurso.Api
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia os arquivos de projeto primeiro para cache de camadas
COPY ["Concurso.Api/Concurso.Api.csproj", "Concurso.Api/"]
COPY ["Concurso.Messaging/Concurso.Messaging.csproj", "Concurso.Messaging/"]
COPY ["Concurso.Consumer/Concurso.Consumer.csproj", "Concurso.Consumer/"]
COPY ["Concurso.Producer/Concurso.Producer.csproj", "Concurso.Producer/"]
COPY ["Concurso.Shared/Concurso.Shared.csproj", "Concurso.Shared/"]

RUN dotnet restore "Concurso.Api/Concurso.Api.csproj"

# Copia o restante dos arquivos e compila
COPY . .
WORKDIR "/src/Concurso.Api"
RUN dotnet publish "Concurso.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Suporta tanto a porta padrão 8080 quanto a porta 10000 injetada pelo Render
EXPOSE 8080
EXPOSE 10000
ENV ASPNETCORE_URLS=http://+:8080;http://+:10000
ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Concurso.Api.dll"]
