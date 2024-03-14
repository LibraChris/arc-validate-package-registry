# Introduction

The **arc-validate-package-registry (avpr)** repository contains:

- a staging area for authoring official validation packages intended for use with [`arc-validate`](https://github.com/nfdi4plants/arc-validate).
- a web API for serving validation packages. This API is consumed by `arc-validate` to install and sync validation packages.
- a website for browsing validation packages.
- some domain types and utilities relevant for consuming libraries in the [AVPRIndex library](./src/AVPRIndex/)
- a .NET client library for consuming the web API in the [AVPRClient library](./src/AVPRClient/)

Read more at [avpr.nfdi4plants.org/about](https://avpr.nfdi4plants.org/about)

# Table of contents

- [General](#general)
- [Validation package staging area](#validation-package-staging-area)
  - [Allowed validation package file formats](#allowed-validation-package-file-formats)
  - [Automated package testing](#automated-package-testing)
  - [The preview package index](#the-preview-package-index)
  - [How to add packages](#how-to-add-packages)
  - [Versioning packages](#versioning-packages)
  - [Package publication workflow](#package-publication-workflow)
- [Package metadata](#package-metadata)
  - [Mandatory fields](#mandatory-fields)
  - [Optional fields](#optional-fields)
    - [Objects](#objects)
      - [Author](#author)
      - [Tag](#tag)
- [Web API (PackageRegistryService)](#web-api-packageregistryservice)
  - [Local development](#local-development)
  - [OpenAPI endpoint documentation via Swagger UI](#openapi-endpoint-documentation-via-swagger-ui)
- [Repository setup](#repository-setup)
  - [local development](#local-development-1)

# General

This repo runs an [extensive CI/CD pipeline](.github/workflows/pipeline.yml) on every commit and PR on the `main` branch. The pipeline includes:

- tests and pre-publish checks for every package in the [staging area](#validation-package-staging-area).
- a release pipeline for validation packages:
  - publishing WIP packages to the `avpr-preview-index` on this repo's [preview-index](https://github.com/nfdi4plants/arc-validate-package-registry/releases/tag/preview-index) release
  - publishing stable packages to the production instance of the web API at [avpr.nfdi4plants.org](https://avpr.nfdi4plants.org)
- tests and release pipelines for the `AVPRIndex` and `AVPRClient` libraries, as well as a docker container for the `PackageRegistryService` web API.

```mermaid
flowchart TD

setup("<b>setup</b> <br> (determines subsequent jobs <br>based on changed files)")
batp("<b>Build and test projects</b><br>any of [AVPRIndex, AVPRClient, API]")
tsa("<b>Test staging area</b><br>test all packages in the staging area")
sapc("<b>Staging area pre-publish checks</b><br>hash verification, prevent double publication etc")
nr("<b>Release (nuget)</b><br>any of [AVPRIndex, AVPRClient]")
dr("<b>Release (docker image)</b><br>API")
upi("<b>Update preview index</b><br>update the github release index json file")
ppp("<b>Publish pending packages</b><br>Publish packages to production DB")

setup --when relevant project<br>  files change--> batp
setup --changes in the<br> staging area--> tsa
batp --when tests pass and<br> release notes change--> nr
batp --when tests pass--> dr
tsa --when tests pass--> sapc
sapc --when checks pass--> upi
sapc --when checks pass<br> and any new packages<br> are pending--> ppp
```

[🔼 Back to top](#table-of-contents)

# Validation package staging area

The [package staging area](./StagingArea) is intended for development and testing of validation packages.

Files in this folder must follow the naming convention `<package-name>@<major>.<minor>.<patch>.*` and contain a [yml frontmatter](#package-metadata) at the start of the file. These files must additionally be inside a subfolder exactly named as the package name. This leads to a folder structure like this:

```no-highlight
StagingArea
│ 
├── some-package
│   ├── some-package@1.0.0.fsx
│   ├── some-package@2.0.0.fsx
│   └── some-package@2.1.0.fsx
│ 
└── some-other-package
    ├── some-other-package@1.0.0.fsx
    ├── some-other-package@2.0.0.fsx
    └── some-other-package@3.0.0.fsx
```

## Allowed validation package file formats

As all reference implementations are written in F#, the only currently allowed file format for validation packages is `.fsx` (F# script files). This can and will be expanded in the future.

## Automated package testing

Tests located at [./tests](./tests) are run on every package in the index. Only if all packages pass these tests, the docker container will be built and pushed to the registry.

## The preview package index

Besides the published packages available at [avpr.nfdi4plants.org](https://avpr.nfdi4plants.org),

The pipeline includes a `Update preview index` CI step that extracts metadata from the [yml frontmatter](#package-metadata) of every `.fsx` file in the staging area and (if tests and sanity checks pass) adds it to the `avpr-preview-index.json` on this repo's [preview-index](https://github.com/nfdi4plants/arc-validate-package-registry/releases/tag/preview-index) release

## How to add packages

To add a package, follow these steps:

- fork this repo
- add a new `.fsx` file to the respective folder in the [staging area](StagingArea/). For more info on the staging area structure, see [Validation package staging area](#validation-package-staging-area).
- commit it to the repo
- open a PR to the `main` branch of this repo

All packages in the staging area are automatically tested on every PR. Additionally, all packages set to `publish: true` in their yml frontmatter will be pushed to the registry service if they pass all tests and are not already present in the registry (see [Package publication workflow](#package-publication-workflow) for more info).

## Versioning packages

Packages SHOULD be versioned according to the [semantic versioning](https://semver.org/) standard. This means that the version number of a package should be incremented according to the following rules:

- **Major version**: incremented when you make changes incompatible with previous versions
- **Minor version**: incremented when you add functionality in a backwards-compatible manner
- **Patch version**: incremented when you make backwards-compatible bug fixes

## Package publication workflow

Publishing a package to the registry is a multi-step process:

Suppose you want to develop version 1.0.0 of a package called `my-package`.

1. Add a new blank `my-package@1.0.0.fsx` file to the [staging area](./StagingArea/) in the folder `my-package`.
2. Develop the package, using a work-in-process pull request to use this repository's CI to perform automated integrity tests on it.
3. Once the package is ready, add `publish: true` to the yml frontmatter of the package file. This will trigger the CI to build and push the package to the registry once the PR is merged.
4. Once a package is published, it cannot be unpublished or changed. To update a package, create a new script with the same name and a higher version number.

| stage | availability | mutability |
| --- | --- | --- |
| staging: development in this repo | version of current HEAD commit in this repo via github API-based execution in `arc-validate` CLI | any changes are allowed |
| published: available in the registry | version of the published package via the registry API | no changes are allowed |

[🔼 Back to top](#table-of-contents)

# Package metadata

Package metadata is extracted from **yml frontmatter** at the start of the `.fsx` file, indicated by a multiline comment (`(* ... *)`)containing the frontmatter fenced by `---` at its start and end:
  
```fsharp
(*
---
<yaml frontmatter here>
---
*)
```

## Mandatory fields

| Field | Type | Description |
| --- | --- | --- |
| Name | string | the name of the package |
| MajorVersion | int | the major version of the package |
| MinorVersion | int | the minor version of the package |
| PatchVersion | int | the patch version of the package |
| Summary | string | a single sentence description (<=50 words) of the package |
| Description | string | an unconstrained free text description of the package |

<details>
<summary>Example: only mandatory fields</summary>

```fsharp
(*
---
Name: my-package
MajorVersion: 1
MinorVersion: 0
PatchVersion: 0
Summary: My package does the thing.
Description: |
  My package does the thing. 
  It does it very good, it does it very well. 
  It does it very fast, it does it very swell.
---
*)
let doSomeValidation () = ()
doSomeValidation ()
```

</details>

## Optional fields

| Field | Type | Description |
| --- | --- | --- |
| Publish | bool | a boolean value indicating whether the package should be published to the registry. If set to `true`, the package will be built and pushed to the registry. If set to `false` (or not present), the package will be ignored. |
| Authors | author[] | the authors of the package. For more information about mandatory and optional fields in this object, see [Objects > Author](#author) |
| Tags | string[] | a list of tags with optional ontology annotations that describe the package. For more information about mandatory and optional fields in this object, see [Objects > Tag](#tag)  |
| ReleaseNotes | string[] | a list of release notes for the package indicating changes from previous versions |

<details>
<summary>Example: all fields</summary>

```fsharp
(*
---
Name: my-package
MajorVersion: 1
MinorVersion: 0
PatchVersion: 0
Summary: My package does the thing.
Description: |
  My package does the thing. 
  It does it very good, it does it very well. 
  It does it very fast, it does it very swell.
Publish: true
Authors:
  - FullName: John Doe
    Email: j@d.com
    Affiliation: University of Nowhere
    AffiliationLink: https://nowhere.edu
  - FullName: Jane Doe
    Email: jj@d.com
    Affiliation: University of Somewhere
    AffiliationLink: https://somewhere.edu
Tags:
  - Name: validation
  - Name: my-tag
    TermSourceREF: my-ontology
    TermAccessionNumber: MO:12345
ReleaseNotes: |
  - initial release
    - does the thing
    - does it well"
---
*)
let doSomeValidation () = ()
doSomeValidation ()
```

</details>

### Objects

#### Author

Author metadata about the people that create and maintain the package. Note that the

| Field | Type | Description | Mandatory |
| --- | --- | --- | --- |
| FullName | string | the full name of the author | yes |
| Email | string | the email address of the author | no |
| Affiliation | string | the affiliation (e.g. institution) of the author | no |
| AffiliationLink | string | a link to the affiliation of the author | no |

#### Tag

Tags can be any string with an optional ontology annotation from a controlled vocabulary:

| Field | Type | Description | Mandatory |
| --- | --- | --- | --- |
| Name | string | the name of the tag | yes |
| TermSourceREF | string | Reference to a controlled vocabulary source | no |
| TermAccessionNumber | string | Accession in the referenced controlled vocabulary source | no |

[🔼 Back to top](#table-of-contents)

# Web API (PackageRegistryService)

The `PackageRegistryService` project located in `/src` is a simple ASP.NET Core (8) web API that serves validation packages and/or associated metadata via a few endpoints.

It is developed specifically for containerization and use in a docker environment. 

The service will eventually be continuously deployed to a public endpoint on the nfdi4plants infrastructure.

## Local development

To run the `PackageRegistryService` locally, ideally use VisualStudio and run the `Docker Compose` project in Debug mode. This will launch the stack defined at [`docker-compose.yml`](docker-compose.yml), which includes:

- the containerized `PackageRegistryService` application 
- a `postgres` database seeded with the [latest indexed packages](src/PackageRegistryService/Data/arc-validate-package-index.json)
- an `adminer` instance for database management (will maybe be replaced by pgAdmin in the future)

## OpenAPI endpoint documentation via Swagger UI

The `PackageRegistryService` has a built-in Swagger UI endpoint for API documentation. It is served at `/swagger/index.html`.

[🔼 Back to top](#table-of-contents)

# Repository setup

## local development

install the following prerequisites:
- .NET 8 SDK
- Docker
- Docker Compose

[🔼 Back to top](#table-of-contents)