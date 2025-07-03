# ClassVR Unity Plugin

The ClassVR Unity Plugin provides utilities and assets to enable development for ClassVR headsets and integration with ClassVR cloud services.

## Usage

```
using ClassVR;
...
// Read ClassVR headset properties
var headsetName = CVRProperties.Instance.DisplayName;

// Read Android launch intent data
var intentAction = AndroidIntent.Instance.Action;
```
