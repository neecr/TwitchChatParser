This program read chat messages from multiple [Twitch](https://twitch.tv) channels and stores them in a Postgres
database.

In order to work, this program requires `appsettings.json` file with the following content:

```json
{
"ClientSecret": "<your Twitch account secret>",
"ClientId": "<your Twitch account id>",
"Username": "<your Twitch account username>",
"ConnectionString": "Host=<DB url>;Port=<DB port>;Database=<DB name>;Username=<DB username>;Password=<DB password>;",
"Channels": [ "<channel1>", "<channel2>", "..." ]
}
```