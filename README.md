# TwitchChatParser

An application that connects to [Twitch](https://twitch.tv) chat across multiple channels, parses chat messages, and stores them in a PostgreSQL database.

## Features

- **Chat Logging**: Connects to specified Twitch channels and logs incoming chat messages.
- **Batch Processing**: Buffers and saves messages to the database in batches to optimize performance.
- **Followers Tracking**: Periodically retrieves and tracks the follower count for users who are active in the chat.
- **Ban Tracking**: Detects and logs user bans in the tracked channels.
- **Auto-reconnect & Token Management**: Automatically handles Twitch OAuth token refreshing and gracefully reconnects on connection failures.

## Configuration

To run the application, you need to provide an `appsettings.json` file in project root directory:

```json
{
  // twitch api credentials
  "TwitchSettings": {
    "ClientId": "your_twitch_client_id",
    "ClientSecret": "your_twitch_client_secret",
    "Username": "your_twitch_bot_username",
    "Channels": [
      "channel1",
      "channel2"
    ]
  },
  "ConnectionStrings": {
    "Debug": "Host=localhost;Database=twitch_chat;Username=postgres;Password=yourpassword",
    "Release": "Host=production_host;Database=twitch_chat;Username=postgres;Password=yourpassword"
  },
  "MessageProcessingSettings": {
    "Interval": 5, // time in seconds to wait before forcing a batch save of messages to the database
    "Buffer": 100, // maximum number of messages to keep in memory before saving them to the database
    "FollowersInfoLifetime": 1 // time in hours before a user's follower count is considered outdated and gets updated.
  }
}
```