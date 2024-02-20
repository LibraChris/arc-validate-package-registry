﻿using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using PackageRegistryService;
using PackageRegistryService.Models;
using PackageRegistryService.Pages;
using Microsoft.AspNetCore.HttpOverrides;
using PackageRegistryService.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PackageRegistryService.API
{
    public static class APIEndpointsV1
    {
        // get all validation packages
        public static async Task<Ok<ValidationPackage[]>> GetAllPackages(ValidationPackageDb database)
        {
            var packages = await database.ValidationPackages.ToArrayAsync();
            return TypedResults.Ok(packages);
        }

        public static async Task<Ok<ValidationPackage>> GetLatestPackageByName(string name, ValidationPackageDb database)
        {
            var package = await database.ValidationPackages
                .Where(p => p.Name == name)
                .OrderByDescending(p => p.MajorVersion)
                .ThenByDescending(p => p.MinorVersion)
                .ThenByDescending(p => p.PatchVersion)
                .FirstOrDefaultAsync();

            return TypedResults.Ok(package);
        }

        public static async Task<Results<BadRequest<string>, NotFound, Ok<ValidationPackage>>> GetPackageByNameAndVersion(string name, string version, ValidationPackageDb database)
        {
            var splt = version.Split('.');
            if (splt.Length != 3)
            {
                return TypedResults.BadRequest("version was not a of valid format MAJOR.MINOR.REVISION");
            }

            int major; int minor; int revision;

            if (
                !int.TryParse(splt[0], out major)
                || !int.TryParse(splt[1], out minor)
                || !int.TryParse(splt[2], out revision)
            )
            {
                return TypedResults.BadRequest("version was not a of valid format MAJOR.MINOR.REVISION");
            }


            var package = await database.ValidationPackages.FindAsync(name, major, minor, revision);
            if (package is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(package);
        }

        public static async Task<Results<Ok<ValidationPackage>, Conflict, UnauthorizedHttpResult>> CreatePackage(ValidationPackage package, ValidationPackageDb database)
        {
            var existing = await database.ValidationPackages.FindAsync(package.Name, package.MajorVersion, package.MinorVersion, package.PatchVersion);
            if (existing != null)
            {
                return TypedResults.Conflict();
            }

            database.ValidationPackages.Add(package);
            await database.SaveChangesAsync();

            return TypedResults.Ok(package);
        }

        public static async Task<Results<Ok, UnprocessableEntity>> Verify(string name, string version, [FromBody] string hash)
        {
            return TypedResults.UnprocessableEntity();
        }

        public static RouteGroupBuilder MapApiV1(this RouteGroupBuilder group)
        {

            // packages endpoints
            group.MapGet("/packages", GetAllPackages)
                .WithOpenApi()
                .WithName("GetAllPackages");

            group.MapGet("/packages/{name}", GetLatestPackageByName)
                .WithOpenApi()
                .WithName("GetLatestPackageByName");

            group.MapGet("/packages/{name}/{version}", GetPackageByNameAndVersion)
                .WithOpenApi()
                .WithName("GetPackageByNameAndVersion");

            group.MapPost("/packages", CreatePackage)
                .WithOpenApi()
                .WithName("CreatePackage")
                .AddEndpointFilter<APIKeyEndpointFilter>(); // creating packages via post requests requires an API key

            // verify endpoints
            group.MapPost("/verify/{name}/{version}", Verify)
                .WithOpenApi()
                .WithName("Verify");

            return group;
        }
    }
}
