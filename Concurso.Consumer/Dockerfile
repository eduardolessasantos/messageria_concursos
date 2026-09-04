FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Concurso.Consumer/Concurso.Consumer.csproj", "Concurso.Consumer/"]
COPY ["Concurso.Messaging/Concurso.Messaging.csproj", "Concurso.Messaging/"]
COPY ["Concurso.Shared/Concurso.Shared.csproj", "Concurso.Shared/"]

RUN dotnet restore "Concurso.Consumer/Concurso.Consumer.csproj"

COPY . .
WORKDIR "/src/Concurso.Consumer"
RUN dotnet publish "Concurso.Consumer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Concurso.Consumer.dll"]
