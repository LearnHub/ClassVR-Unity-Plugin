# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Cloud file metadata support in the gRPC clients
- `CloudFile.IconUrl` (replacing the deprecated `CloudFile.PreviewUrl`), and an `IconSpec` on the cloud file get/search requests
- `CloudFileQuery.IconSize` and the `CloudIconSize` enum, to request an icon for each cloud file result
- `CloudFile.MetadataCount`, so callers can tell whether a file has metadata without fetching it
- Client credential support on `Authorization` via the new `ClientCredentials` message
- `CloudFiles.Upload` and `CloudUploadResult`, which expose the uploaded file's `EntityId` — the key needed to attach cloud file metadata

### Deprecated

- `CloudFile.PreviewUrl` on the cloud file query API. Use `CloudFile.IconUrl`, which carries the identical value
- `FileUploader.UploadToSharedCloud`. Use `CloudFiles.Upload`, which additionally returns the `EntityId`. Behaviour is otherwise unchanged

### Changed

- Regenerated C# gRPC clients to add support for cloud file metadata
- Analytics events now send client credentials on the `Authorization` rather than the deprecated `RecordActionRequest.Client` field
- **Breaking:** `Authorization.Credentials` renamed to `Authorization.ConnectionCredentials`
- **Breaking:** `PreviewSpec` renamed to `IconSpec` on `GetCloudFilesRequest` and `SearchCloudFilesRequest`
- **Breaking:** `CloudFile.Id` renamed to `CloudFile.EntityId`, matching `CloudUploadResult.EntityId`

### Removed

- **Breaking:** `CreateClientCredentialsRequest.ClientState`

### Fixed

- Shared Cloud filenames being lost when uploading content already present in AVNFS

## [2.1.0] - 2026-06-19

### Added

- ClassVR Shared Cloud query API to search files in AVNFS associated with an organisation
- FileUploader.UploadToAvnfs overloads (string, byte array, and file path) to upload a file to AVNFS without associating it with an organisation

### Changed

- Regenerated C# gRPC clients to add speaker ID into speech synthesis request
- Upgraded CVR Unity Java Plugin to version 8 to fix compile issue in Unity 2021.3
- Package.json to specify Unity 2021.3 as min version

## [2.0.1] - 2026-03-23

### Fixed

- Add .meta file for OpenXR directory

## [2.0.0] - 2026-03-20

### Added

- Tilt to spin OpenXR feature with API to disable/enable at runtime
- Optional JWT argument to FileUploader.UploadToSharedCloud that falls back to using CVRProperties (only available on Android)
- FileUploader.UploadToSharedCloud overload which takes a filepath
- Low-level (plumbing) class AvnfsUploader for file upload on Android via the AVNFS ContentProvider
- ContentProviderClient for file retrieval on Android via URI from a ContentProvider
- ClassVR 655 headset interaction profile support
- System utils OpenXR feature with API to get time since last recenter event

### Changed

- Upgraded CVR Unity Java Plugin to version 7
- FileUploader.UploadToSharedCloud uses AVNFS ContentProvider for upload on Android, instead of gRPC + HTTP POST
- Regenerated C# gRPC clients for AVNCloud
- Namespaces of AVNCloud and Android Platform scripts

## [1.5.0] - 2025-11-04

### Added

- IntentProvider class with event for receiving new intents
- DeepLinkActivity which notifies when it receives new intents

### Changed

- Upgraded CVR Unity Java Plugin to version 6

### Deprecated

- AndroidIntent.Instance is obsolete as it is no longer a singleton. Functionality remains unchanged for backwards compatibility.

## [1.4.0] - 2025-10-23

### Added

- Methods for uploading files to the ClassVR Shared Cloud area for the current organisation

## [1.3.0] - 2025-09-16

### Added

- Updated AVNCloud gRPC clients to add XLIFF translation support

## [1.2.0] - 2025-08-13

### Added

- API for logging analytics events to AVN Cloud

## [1.1.0] - 2025-08-04

### Added

- API for querying the system locale

### Changed

- CVR Unity Java Plugin to version 5

## [1.0.0] - 2025-07-03

### Added

- Unity package layout
- API for querying ClassVR device properties
- API for querying the Android launch Intent
