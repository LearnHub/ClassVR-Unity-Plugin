# ClassVR Unity Plugin

The ClassVR Unity Plugin provides utilities and assets to enable development for ClassVR headsets and integration with ClassVR cloud services.

## Installation

Unless you intend to make changes to the Plugin, please install via [OpenUPM](https://openupm.com/packages/com.avantis.classvr/). OpenUPM installation instructions are here - https://openupm.com/packages/com.avantis.classvr/#modal-manualinstallation.

## Usage

```
using ClassVR;
...
// Read ClassVR headset properties
var headsetName = CVRProperties.Instance.DisplayName;

// Read Android launch intent data
var intentAction = AndroidIntent.Instance.Action;

// Read the system locale
var sysLocale = SystemProperties.GetSysLocale();

// Send an analytics event (fire and forget)
_ = Analytics.SendEvent("example_action", "example_source");
// Send an analytics event (and wait for it to complete)
await Analytics.SendEvent("example_action", "example_source");
```
