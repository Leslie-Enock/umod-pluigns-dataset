# uMod Parser

A parser for collecting information about plugins from the uMod.org platform.

## Features

- Collection of information about all available plugins
- Retrieval of all versions for each plugin
- Saving data in JSON format
- Error handling and logging
- Protection against blocking with request delays

## Installation

1. Clone the repository
2. Install dependencies:
```bash
pip install -r requirements.txt
```

## Usage

Run the script:
```bash
python umod_parser.py
```

Data will be saved in the `plugins_data` directory in JSON format, one file per plugin.

## Data Structure

Each JSON file contains the following information about a plugin:

```json
{
  "id": "plugin-id",
  "name": "Plugin Name",
  "author": "Author Name",
  "description": "Plugin description",
  "categories": ["category1", "category2"],
  "total_downloads": 1000,
  "latest_version": "1.0.0",
  "created_at": "2024-02-20T12:00:00Z",
  "updated_at": "2024-02-20T12:00:00Z",
  "versions": [
    {
      "version": "1.0.0",
      "released_at": "2024-02-20T12:00:00Z",
      "download_url": "https://umod.org/plugins/plugin-id/download/1.0.0",
      "changelog": "Version changelog"
    }
  ]
}
```

## Logging

Logs are output to the console and contain information about:
- Parsing process (current page)
- Request errors
- Data parsing errors
- Total number of processed plugins 