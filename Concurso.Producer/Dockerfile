FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Concurso.Producer/Concurso.Producer.csproj", "Concurso.Producer/"]
COPY ["Concurso.Messaging/Concurso.Messaging.csproj", "Concurso.Messaging/"]
COPY ["Concurso.Shared/Concurso.Shared.csproj", "Concurso.Shared/"]

RUN dotnet restore "Concurso.Producer/Concurso.Producer.csproj"

COPY . .
WORKDIR "/src/Concurso.Producer"
RUN dotnet publish "Concurso.Producer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Concurso.Producer.dll"]
