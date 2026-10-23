FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY HrSystemDhg.Web/ HrSystemDhg.Web/
RUN dotnet publish HrSystemDhg.Web -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
COPY docker-entrypoint.sh .
RUN chmod +x docker-entrypoint.sh && mkdir -p App_Data /home/app/.aspnet/DataProtection-Keys && chown -R app:app /app /home/app/.aspnet
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["./docker-entrypoint.sh"]
