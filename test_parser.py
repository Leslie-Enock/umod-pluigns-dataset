import requests
import json
from typing import Dict, List, Optional
from dataclasses import dataclass
from datetime import datetime
import time
from pathlib import Path
import logging
import os

logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

@dataclass
class PluginVersion:
    version: str
    released_at: datetime
    download_url: str
    changelog: Optional[str] = None

@dataclass
class Plugin:
    id: str
    name: str
    author: str
    description: str
    versions: List[PluginVersion]
    categories: List[str]
    total_downloads: int
    latest_version: str
    created_at: datetime
    updated_at: datetime

class UmodParser:
    BASE_URL = "https://umod.org"
    SEARCH_URL = f"{BASE_URL}/plugins/search.json"
    PLUGIN_URL = f"{BASE_URL}/plugins"
    
    def __init__(self, output_dir: str = "plugins_data"):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        logger.info(f"Created output directory: {os.path.abspath(self.output_dir)}")
        
        self.session = requests.Session()
        # Заголовки для имитации браузера
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36',
            'Accept': 'application/json',
            'Referer': 'https://umod.org/plugins'
        })
        
    def _make_request(self, url: str, params: Dict = None) -> Dict:
        """Выполняет HTTP запрос с обработкой ошибок и минимальной задержкой"""
        try:
            logger.debug(f"Making request to {url}")
            response = self.session.get(url, params=params)
            response.raise_for_status()
            return response.json()
        except Exception as e:
            logger.error(f"Error making request to {url}: {e}")
            raise

    def get_test_plugins(self, limit: int = 3) -> List[Plugin]:
        """Получает ограниченное количество плагинов для тестирования"""
        logger.info(f"Getting {limit} test plugins")
        
        # Получаем первую страницу
        params = {
            "page": 1,
            "per_page": limit,
            "sort": "title",
            "sortdir": "asc",
            "categories[0]": "universal",
            "categories[1]": "rust"
        }
        
        data = self._make_request(self.SEARCH_URL, params)
        plugins = []
        
        for plugin_data in data.get("data", [])[:limit]:
            try:
                plugin = self._process_plugin(plugin_data)
                if plugin:
                    plugins.append(plugin)
                    self._save_plugin_data(plugin)
            except Exception as e:
                logger.error(f"Error processing plugin: {e}", exc_info=True)
                
        return plugins

    def _process_plugin(self, data: Dict) -> Optional[Plugin]:
        """Обрабатывает данные одного плагина"""
        plugin_id = str(data.get("slug", ""))
        if not plugin_id:
            logger.error("Plugin slug is missing")
            return None
            
        logger.info(f"Processing plugin: {plugin_id}")
        
        name = str(data.get("name", data.get("title", "")))
        author = data.get("author", "Unknown")
        description = str(data.get("description", ""))
        
        # Получаем основные данные из метаданных
        latest_version = str(data.get("latest_release_version", ""))
        total_downloads = int(data.get("downloads", 0))
        
        # Простая версия без запроса дополнительных версий
        versions = []
        versions.append(PluginVersion(
            version=latest_version or "unknown",
            released_at=datetime.now(),
            download_url=f"{self.PLUGIN_URL}/{plugin_id}/download/latest",
            changelog=None
        ))
        
        created_at_str = data.get("created_at", "")
        updated_at_str = data.get("updated_at", "")
        
        try:
            created_at = datetime.strptime(created_at_str, "%Y-%m-%d %H:%M:%S")
            updated_at = datetime.strptime(updated_at_str, "%Y-%m-%d %H:%M:%S")
        except ValueError:
            logger.warning(f"Invalid date format for plugin {plugin_id}, using current time")
            created_at = updated_at = datetime.now()
        
        category_tags = data.get("category_tags", "")
        categories = category_tags.split(",") if category_tags else []
        
        return Plugin(
            id=plugin_id,
            name=name,
            author=author,
            description=description,
            versions=versions,
            categories=categories,
            total_downloads=total_downloads,
            latest_version=latest_version,
            created_at=created_at,
            updated_at=updated_at
        )

    def _save_plugin_data(self, plugin: Plugin):
        """Сохраняет данные плагина в JSON файл"""
        plugin_file = self.output_dir / f"{plugin.id}.json"
        logger.info(f"Saving plugin data to {os.path.abspath(plugin_file)}")
        
        plugin_data = {
            "id": plugin.id,
            "name": plugin.name,
            "author": plugin.author,
            "description": plugin.description,
            "categories": plugin.categories,
            "total_downloads": plugin.total_downloads,
            "latest_version": plugin.latest_version,
            "created_at": plugin.created_at.isoformat(),
            "updated_at": plugin.updated_at.isoformat(),
            "versions": [
                {
                    "version": v.version,
                    "released_at": v.released_at.isoformat(),
                    "download_url": v.download_url,
                    "changelog": v.changelog
                }
                for v in plugin.versions
            ]
        }
        
        with open(plugin_file, "w", encoding="utf-8") as f:
            json.dump(plugin_data, f, ensure_ascii=False, indent=2)
            
        # Проверка, что файл действительно создан
        if plugin_file.exists():
            logger.info(f"Successfully saved plugin data: {plugin.name}")
            logger.info(f"File size: {plugin_file.stat().st_size} bytes")
        else:
            logger.error(f"Failed to save plugin data: {plugin.name}")

def main():
    parser = UmodParser()
    try:
        # Получаем только 3 плагина для теста
        plugins = parser.get_test_plugins(limit=3)
        logger.info(f"Successfully parsed {len(plugins)} plugins")
        
        # Выводим содержимое директории
        output_dir = Path("plugins_data")
        if output_dir.exists():
            files = list(output_dir.glob("*.json"))
            logger.info(f"Files in output directory: {len(files)}")
            for file in files:
                logger.info(f"  - {file.name} ({file.stat().st_size} bytes)")
    except Exception as e:
        logger.error(f"Error during parsing: {e}")

if __name__ == "__main__":
    main() 