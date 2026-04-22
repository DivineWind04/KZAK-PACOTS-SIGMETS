# PACOTS/SIGMETS/NOTAMS Plugin

A vatSys plugin for displaying PACOTS tracks, SIGMETs, and NOTAMs for the Pacific region (KZAK/Oakland FIR).

## Features

- **PACOTS Tracks**: Parses Pacific Organized Track System tracks directly from FAA NMS-API NOTAMs
- **SIGMETs**: Retrieves international SIGMETs from Aviation Weather API
- **NOTAMs**: Full integration with FAA NMS-API for NOTAM retrieval

## Configuration

### FAA NMS-API Credentials

Configure your FAA API credentials in `app.config`:

```xml
<appSettings>
    <add key="NotamApi:ClientId" value="YOUR_CLIENT_ID" />
    <add key="NotamApi:ClientSecret" value="YOUR_CLIENT_SECRET" />
    <add key="NotamApi:AuthUrl" value="https://api-sit.cgifederal-aim.com/v1/auth/token" />
    <add key="NotamApi:BaseUrl" value="https://api-staging.cgifederal-aim.com/nmsapi/v1" />
</appSettings>
```

### Obtaining FAA API Credentials

1. Contact CGI/FAA to request API access for the NMS-API
2. You will receive a `client_id` and `client_secret`
3. The API uses OAuth2 client credentials flow for authentication
4. Tokens expire after ~30 minutes and are automatically refreshed

## How It Works

1. Plugin fetches NOTAMs from FAA NMS-API for KZAK and RJJJ FIRs
2. PACOTS-related NOTAMs (containing "PACOTS", "TDM TRK", etc.) are parsed
3. Track waypoints are extracted (5-letter fixes and coordinate formats like 42N160E)
4. Tracks are displayed on the vatSys map as restricted areas
5. Data refreshes every 15 minutes automatically

## Usage

The plugin automatically fetches and updates:
- PACOTS tracks every 15 minutes
- SIGMETs every 15 minutes
- All NOTAMs for Pacific region

### Programmatic Access

```csharp
// Get NOTAMs for specific locations
var notams = await Plugin.GetNotamsForLocationsAsync(new[] { "KZAK", "RJJJ" }, minutesBack: 60);

// Get NOTAMs by classification
var filteredNotams = await Plugin.GetNotamsByClassificationAsync(new[] { "AIRSPACE" });

// Test API connectivity
bool isConnected = await Plugin.TestNotamApiConnectionAsync();

// Manually refresh tracks
await Plugin.RefreshTracksFromNotamApiAsync();
```

## Building

Requires:
- .NET Framework 4.8
- vatSys reference (vatSys.exe)

## License

Work in progress.
