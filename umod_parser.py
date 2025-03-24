import requests
import json
from typing import Dict, List, Optional
from dataclasses import dataclass
from datetime import datetime
import time
from pathlib import Path
import logging
from concurrent.futures import ThreadPoolExecutor, as_completed
import random
import threading
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
    
    def __init__(self, output_dir: str = "plugins_data", max_workers: int = 3, max_pages: int = 3):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        logger.info(f"Created/using output directory: {os.path.abspath(self.output_dir)}")
        
        self.session = requests.Session()
        self.max_workers = max_workers
        self.max_pages = max_pages  # Ограничиваем количество страниц для тестирования
        
        # Добавляем заголовки для имитации браузера
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36',
            'Accept': 'application/json',
            'Accept-Language': 'en-US,en;q=0.9',
            'Referer': 'https://umod.org/plugins',
            'Cache-Control': 'no-cache'
        })
        
        # Добавляем семафор для ограничения одновременных запросов
        self.request_semaphore = threading.Semaphore(2)
        
    def _make_request(self, url: str, params: Dict = None) -> Dict:
        """Выполняет HTTP запрос с обработкой ошибок и задержкой"""
        max_retries = 3
        retry_delay = 10  # увеличенная начальная задержка
        
        # Добавляем случайную задержку для имитации человеческого поведения
        jitter = random.uniform(1.5, 4.0)
        time.sleep(jitter)
        
        # Используем семафор для ограничения количества одновременных запросов
        with self.request_semaphore:
            for attempt in range(max_retries):
                try:
                    logger.debug(f"Making request to {url}")
                    response = self.session.get(url, params=params)
                    
                    if response.status_code == 429:
                        wait_time = retry_delay * (2 ** attempt) + random.uniform(1, 5)
                        logger.warning(f"Rate limit hit, waiting {wait_time:.2f} seconds before retry")
                        time.sleep(wait_time)
                        continue
                        
                    response.raise_for_status()
                    
                    # Добавляем паузу после успешного запроса
                    time.sleep(3 + random.uniform(0, 2))
                    
                    return response.json()
                except requests.exceptions.RequestException as e:
                    if attempt == max_retries - 1:  # последняя попытка
                        logger.error(f"Error making request to {url}: {e}")
                        raise
                    wait_time = retry_delay * (2 ** attempt) + random.uniform(1, 5)
                    logger.warning(f"Request failed, retrying in {wait_time:.2f} seconds...")
                    time.sleep(wait_time)
            
            raise Exception("Max retries exceeded")

    def get_all_plugins(self) -> List[Plugin]:
        """Получает список плагинов с использованием bulk-загрузки (ограничено для тестирования)"""
        all_plugin_data = []
        page = 1
        per_page = 10  # Уменьшаем размер страницы для снижения нагрузки
        
        # Сначала получаем метаданные плагинов (ограниченное количество страниц)
        while page <= self.max_pages:
            logger.info(f"Fetching bulk page {page} (max: {self.max_pages})")
            params = {
                "page": page,
                "per_page": per_page,
                "sort": "title",
                "sortdir": "asc",
                "categories[0]": "universal",
                "categories[1]": "rust"
            }
            
            try:
                data = self._make_request(self.SEARCH_URL, params)
                if not data.get("data"):
                    logger.warning("No data in response")
                    break
                
                page_data = data["data"]
                logger.info(f"Received {len(page_data)} plugins on page {page}")
                all_plugin_data.extend(page_data)
                
                total_pages = min(int(data.get("last_page", 1)), self.max_pages)
                logger.info(f"Fetched page {page} of {total_pages} ({len(page_data)} plugins)")
                
                if page >= total_pages:
                    break
                    
                page += 1
                # Увеличенная задержка между страницами
                time.sleep(5 + random.uniform(1, 3))
            except Exception as e:
                logger.error(f"Error fetching bulk page {page}: {e}")
                break

        logger.info(f"Total plugins found: {len(all_plugin_data)}")
        
        # Обрабатываем данные пакетами для снижения нагрузки
        batch_size = 5  # Уменьшаем размер пакета
        plugins = []
        
        for i in range(0, len(all_plugin_data), batch_size):
            batch = all_plugin_data[i:i+batch_size]
            logger.info(f"Processing batch {i//batch_size + 1} of {(len(all_plugin_data) + batch_size - 1) // batch_size}")
            
            with ThreadPoolExecutor(max_workers=self.max_workers) as executor:
                future_to_plugin = {
                    executor.submit(self._process_plugin, plugin_data): plugin_data
                    for plugin_data in batch
                }
                
                for future in as_completed(future_to_plugin):
                    plugin_data = future_to_plugin[future]
                    try:
                        plugin = future.result()
                        if plugin:
                            plugins.append(plugin)
                            self._save_plugin_data(plugin)
                    except Exception as e:
                        logger.error(f"Error processing plugin {plugin_data.get('name', 'unknown')}: {e}")
            
            # Выводим текущую статистику после каждого пакета
            self._print_current_stats()
            
            # Добавляем паузу между пакетами
            if i + batch_size < len(all_plugin_data):
                wait_time = 10 + random.uniform(1, 5)
                logger.info(f"Waiting {wait_time:.2f} seconds before processing next batch...")
                time.sleep(wait_time)
        
        return plugins
        
    def _print_current_stats(self):
        """Выводит текущую статистику по обработанным файлам"""
        if self.output_dir.exists():
            files = list(self.output_dir.glob("*.json"))
            logger.info(f"Current files in output directory: {len(files)}")
            if len(files) > 0:
                logger.info(f"Last 3 files: {', '.join([f.name for f in files[-3:]])}")
                total_size = sum(f.stat().st_size for f in files)
                logger.info(f"Total data size: {total_size} bytes")

    def _process_plugin(self, data: Dict) -> Optional[Plugin]:
        """Обрабатывает данные одного плагина"""
        try:
            plugin_id = str(data.get("slug", ""))
            if not plugin_id:
                logger.error("Plugin slug is missing")
                return None
            
            logger.info(f"Processing plugin: {plugin_id}")    
            name = str(data.get("name", data.get("title", "")))
            if not name:
                logger.error(f"Plugin name is missing for slug: {plugin_id}")
                return None
            
            author = data.get("author", "Unknown")
            description = str(data.get("description", ""))
            
            # Если версия и информация о загрузках уже есть в метаданных,
            # можно избежать дополнительного запроса
            latest_version = str(data.get("latest_release_version", ""))
            total_downloads = int(data.get("downloads", 0))
            
            # Упрощаем для тестирования - не запрашиваем дополнительные данные о версиях
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
            
            # Разделяем категории, обрабатывая пустую строку
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
        except Exception as e:
            logger.error(f"Error processing plugin data: {e}", exc_info=True)
            return None

    def _get_plugin_versions(self, plugin_id: str) -> List[PluginVersion]:
        """Получает все версии плагина"""
        versions = []
        url = f"{self.PLUGIN_URL}/{plugin_id}/latest.json"
        
        try:
            data = self._make_request(url)
            # Добавляем последнюю версию
            # Обработка различных форматов дат
            created_at_str = data.get("created_at", "")
            if created_at_str:
                try:
                    if "Z" in created_at_str:
                        released_at = datetime.fromisoformat(created_at_str.replace("Z", "+00:00"))
                    else:
                        released_at = datetime.strptime(created_at_str, "%Y-%m-%d %H:%M:%S")
                except ValueError:
                    logger.warning(f"Invalid date format: {created_at_str}, using current time")
                    released_at = datetime.now()
            else:
                released_at = datetime.now()
                
            version = PluginVersion(
                version=data.get("version", "unknown"),
                released_at=released_at,
                download_url=f"{self.PLUGIN_URL}/{plugin_id}/download/{data.get('version', 'latest')}",
                changelog=data.get("changelog")
            )
            versions.append(version)
        except Exception as e:
            logger.error(f"Error getting versions for plugin {plugin_id}: {e}")
        
        return versions

    def _save_plugin_data(self, plugin: Plugin):
        """Сохраняет данные плагина в JSON файл"""
        plugin_file = self.output_dir / f"{plugin.id}.json"
        logger.info(f"Saving plugin data to {plugin.name} ({plugin.id})")
        
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
        
        try:
            with open(plugin_file, "w", encoding="utf-8") as f:
                json.dump(plugin_data, f, ensure_ascii=False, indent=2)
                
            # Проверка, что файл действительно создан
            if plugin_file.exists():
                logger.info(f"Successfully saved plugin data: {plugin.name} ({plugin_file.stat().st_size} bytes)")
            else:
                logger.error(f"File not found after save attempt: {plugin.name}")
        except Exception as e:
            logger.error(f"Error saving plugin data for {plugin.name}: {e}", exc_info=True)

def main():
    # Уменьшаем количество параллельных воркеров и ограничиваем страницы
    parser = UmodParser(max_workers=2, max_pages=100000)
    try:
        plugins = parser.get_all_plugins()
        logger.info(f"Successfully parsed {len(plugins)} plugins")
        
        # Выводим итоговую статистику
        output_dir = Path("plugins_data")
        if output_dir.exists():
            files = list(output_dir.glob("*.json"))
            logger.info(f"Total files in output directory: {len(files)}")
            total_size = sum(f.stat().st_size for f in files)
            logger.info(f"Total data size: {total_size} bytes")
            
            if len(files) > 0:
                logger.info("Sample files:")
                for file in files[:5]:  # Показываем первые 5 файлов
                    logger.info(f"  - {file.name} ({file.stat().st_size} bytes)")
    except Exception as e:
        logger.error(f"Error during parsing: {e}")

if __name__ == "__main__":
    main() 