This program read chat messages from multiple [Twitch](https://twitch.tv) channels and stores them in a Postgres
database.

In order to work, this program requires `appsettings.json` file with the following content:

```json
{
  "TwitchSettings": {
    "ClientSecret": "",
    "ClientId": "",
    "Username": "",
    "Channels": [
      "",
      ""
    ]
  },
  "ConnectionString": "",
  "MessageProcessingSettings": {
    "Interval": 1,
    "Buffer": 1
  }
}
```