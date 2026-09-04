FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia arquivos csproj para otimização de cache do Docker
COPY ["Concurso.Api/Concurso.Api.csproj", "Concurso.Api/"]
COPY ["Concurso.Messaging/Concurso.Messaging.csproj", "Concurso.Messaging/"]
COPY ["Concurso.Consumer/Concurso.Consumer.csproj", "Concurso.Consumer/"]
COPY ["Concurso.Producer/Concurso.Producer.csproj", "Concurso.Producer/"]
COPY ["Concurso.Shared/Concurso.Shared.csproj", "Concurso.Shared/"]

RUN dotnet restore "Concurso.Api/Concurso.Api.csproj"

# Copia todo o código fonte e compila em modo Release
COPY . .
WORKDIR "/src/Concurso.Api"
RUN dotnet publish "Concurso.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Concurso.Api.dll"]
