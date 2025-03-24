import os
import json
import requests
import time
import random
import logging
import shutil
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor, as_completed
from typing import Dict, List, Optional, Tuple
import re

# Настройка логирования
logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class PluginOrganizer:
    """Класс для организации плагинов в новую структуру"""
    
    def __init__(self, source_dir: str = "plugins_data", output_dir: str = "plugins", max_workers: int = 3):
        self.source_dir = Path(source_dir)
        self.output_dir = Path(output_dir)
        self.max_workers = max_workers
        
        # Создаем основную директорию
        self.output_dir.mkdir(parents=True, exist_ok=True)
        logger.info(f"Created output directory: {os.path.abspath(self.output_dir)}")
        
        # Настройка сессии запросов
        self.session = requests.Session()
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36',
            'Accept': 'text/plain,application/json,text/html',
            'Accept-Language': 'en-US,en;q=0.9',
            'Referer': 'https://umod.org/plugins',
            'Cache-Control': 'no-cache'
        })
    
    def organize_plugins(self, limit: Optional[int] = None):
        """Организует все плагины из source_dir в output_dir"""
        # Получаем список JSON файлов
        json_files = list(self.source_dir.glob("*.json"))
        
        if limit:
            json_files = json_files[:limit]
            
        total_files = len(json_files)
        logger.info(f"Found {total_files} plugin JSON files to organize")
        
        # Обрабатываем файлы с помощью ThreadPoolExecutor
        processed_count = 0
        failed_count = 0
        
        # Обрабатываем файлы последовательно для избежания rate limit
        for json_file in json_files:
            try:
                success = self.process_plugin_file(json_file)
                if success:
                    processed_count += 1
                else:
                    failed_count += 1
            except Exception as e:
                logger.error(f"Error processing {json_file.name}: {e}")
                failed_count += 1
            
            # Выводим прогресс
            logger.info(f"Progress: {processed_count + failed_count}/{total_files} ({processed_count} successful, {failed_count} failed)")
            
            # Добавляем задержку между обработкой плагинов
            time.sleep(random.uniform(0.5, 2.0))
        
        logger.info(f"Completed organizing {processed_count} plugins with {failed_count} failures")
    
    def process_plugin_file(self, json_file: Path) -> bool:
        """Обрабатывает один JSON файл с данными плагина"""
        try:
            # Загружаем JSON
            with open(json_file, 'r', encoding='utf-8') as f:
                plugin_data = json.load(f)
            
            plugin_id = plugin_data.get("id")
            plugin_name = plugin_data.get("name")
            
            if not plugin_id or not plugin_name:
                logger.error(f"Missing ID or name in {json_file.name}")
                return False
            
            # Безопасное имя директории
            safe_name = self.get_safe_directory_name(plugin_name)
            
            # Создаем директорию для плагина
            plugin_dir = self.output_dir / safe_name
            plugin_dir.mkdir(exist_ok=True)
            
            # Сохраняем JSON файл
            json_target = plugin_dir / f"{safe_name}.json"
            shutil.copy2(json_file, json_target)
            
            # Получаем URL страницы плагина
            plugin_url = plugin_data.get("url", f"https://umod.org/plugins/{plugin_id}")
            
            # Загружаем .cs файл
            cs_file_url, cs_filename = self.get_plugin_download_url(plugin_data, plugin_url)
            cs_target_path = plugin_dir / f"{safe_name}.cs"
            
            if cs_file_url:
                self.download_plugin_code(cs_file_url, cs_target_path)
            
            # Попытка загрузить README и другие документы
            readme_created = self.try_get_documentation(plugin_url, plugin_dir)
            
            # Если не удалось создать README из страницы, создаем из JSON
            if not (plugin_dir / "README.md").exists():
                self.create_readme_from_json(plugin_data, plugin_dir)
            
            # Создаем директорию для старых версий
            old_versions_dir = plugin_dir / "oldVersions"
            old_versions_dir.mkdir(exist_ok=True)
            
            # Пытаемся найти старые версии
            self.try_get_old_versions(plugin_url, plugin_data, old_versions_dir, safe_name)
            
            logger.info(f"Successfully organized plugin: {plugin_name}")
            return True
            
        except Exception as e:
            logger.error(f"Error organizing plugin from {json_file.name}: {e}")
            return False
    
    def get_safe_directory_name(self, name: str) -> str:
        """Преобразует имя плагина в безопасное имя директории"""
        # Убираем недопустимые символы
        safe_name = re.sub(r'[\\/*?:"<>|]', "", name)
        # Заменяем пробелы на подчеркивания
        safe_name = safe_name.replace(' ', '_')
        return safe_name
    
    def get_plugin_download_url(self, plugin_data: Dict, plugin_url: str) -> Tuple[Optional[str], Optional[str]]:
        """Получает URL для загрузки исходного кода плагина"""
        # Сначала пробуем URL для прямой загрузки .cs файла
        plugin_id = plugin_data.get("id")
        plugin_name = plugin_data.get("name")
        download_url = None
        filename = None
        
        # Формируем URL по шаблону
        if plugin_id:
            # Пытаемся использовать прямой URL для загрузки .cs файла
            download_url = f"https://umod.org/plugins/{plugin_id}.cs"
            filename = f"{plugin_id}.cs"
        
        # Если не можем использовать прямой URL, пытаемся извлечь из страницы
        if not download_url or not plugin_id:
            try:
                time.sleep(random.uniform(1, 3))
                response = self.session.get(plugin_url)
                response.raise_for_status()
                
                html_content = response.text
                
                # Ищем ссылку на загрузку
                download_pattern = r'href="([^"]+\.cs)"'
                matches = re.findall(download_pattern, html_content)
                
                if matches:
                    for match in matches:
                        if "download" in match or "plugins" in match:
                            download_url = match if match.startswith('http') else f"https://umod.org{match}"
                            filename = download_url.split('/')[-1]
                            break
            except Exception as e:
                logger.error(f"Failed to extract download URL from page: {e}")
        
        # Если все еще нет URL, используем стандартный формат с именем плагина
        if not download_url and plugin_name:
            download_url = f"https://umod.org/plugins/{plugin_name.replace(' ', '-').lower()}.cs"
            filename = f"{plugin_name}.cs"
        
        # Логируем URL, который будем использовать
        logger.debug(f"Using download URL: {download_url}")
        
        return download_url, filename
    
    def download_plugin_code(self, url: str, target_path: Path) -> bool:
        """Загружает исходный код плагина"""
        try:
            # Добавляем случайную задержку для избежания блокировки
            time.sleep(random.uniform(1, 3))
            
            logger.debug(f"Downloading plugin code from {url}")
            
            # Используем stream=True для постепенной загрузки
            with self.session.get(url, stream=True) as response:
                response.raise_for_status()
                
                # Открываем файл в бинарном режиме для записи
                with open(target_path, 'wb') as f:
                    # Загружаем и записываем данные частями
                    for chunk in response.iter_content(chunk_size=8192):
                        if chunk:  # фильтруем keep-alive новые куски
                            f.write(chunk)
            
            # Теперь проверяем содержимое файла
            with open(target_path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
                
                # Проверяем, что содержимое похоже на C# код
                if "using " in content and ("class " in content or "namespace " in content):
                    logger.info(f"Downloaded plugin code to {target_path} ({target_path.stat().st_size} bytes)")
                    return True
                else:
                    logger.warning(f"Downloaded content doesn't look like C# code, saving anyway: {url}")
                    return False
                    
        except Exception as e:
            logger.error(f"Failed to download plugin code from {url}: {e}")
            # Создаем пустой файл, чтобы отметить, что мы пытались
            with open(target_path, 'w', encoding='utf-8') as f:
                f.write(f"// Failed to download plugin code: {str(e)}")
            return False
    
    def try_get_documentation(self, plugin_url: str, plugin_dir: Path) -> bool:
        """Пытается получить README и другую документацию"""
        try:
            # Добавляем случайную задержку
            time.sleep(random.uniform(1, 3))
            
            logger.debug(f"Checking for documentation at {plugin_url}")
            response = self.session.get(plugin_url)
            response.raise_for_status()
            
            html_content = response.text
            
            # Ищем ссылки на документацию
            readme_pattern = r'href="([^"]+(?:README|readme)\.md)"'
            docs_pattern = r'href="([^"]+/(?:docs|doc|documentation)/[^"]+\.(?:md|txt))"'
            
            readme_matches = re.findall(readme_pattern, html_content)
            docs_matches = re.findall(docs_pattern, html_content)
            
            # Загружаем README если нашли
            for url in readme_matches:
                full_url = url if url.startswith('http') else f"https://umod.org{url}"
                self.download_documentation(full_url, plugin_dir / "README.md")
                break
            
            # Создаем документацию если есть
            if docs_matches:
                docs_dir = plugin_dir / "docs"
                docs_dir.mkdir(exist_ok=True)
                
                for i, url in enumerate(docs_matches[:5]):  # Ограничиваем количество
                    full_url = url if url.startswith('http') else f"https://umod.org{url}"
                    filename = url.split('/')[-1]
                    self.download_documentation(full_url, docs_dir / filename)
            
            # Ищем информацию о плагине на странице и создаем README если не нашли
            if not readme_matches:
                self.create_readme_from_page(html_content, plugin_dir, plugin_url)
            
            return True
        except Exception as e:
            logger.error(f"Failed to check for documentation at {plugin_url}: {e}")
            return False
    
    def create_readme_from_page(self, html_content: str, plugin_dir: Path, plugin_url: str):
        """Создает README.md из информации на странице плагина"""
        try:
            # Ищем описание плагина
            description_pattern = r'<div class="description">(.*?)</div>'
            description_match = re.search(description_pattern, html_content, re.DOTALL)
            
            # Ищем заголовок
            title_pattern = r'<h1>(.*?)</h1>'
            title_match = re.search(title_pattern, html_content)
            
            if title_match or description_match:
                readme_path = plugin_dir / "README.md"
                
                with open(readme_path, 'w', encoding='utf-8') as f:
                    if title_match:
                        f.write(f"# {title_match.group(1).strip()}\n\n")
                    
                    f.write(f"Plugin URL: {plugin_url}\n\n")
                    
                    if description_match:
                        # Очищаем HTML теги
                        description = re.sub(r'<[^>]+>', '', description_match.group(1))
                        description = description.strip()
                        f.write("## Description\n\n")
                        f.write(f"{description}\n")
                
                logger.info(f"Created README.md from page information")
                return True
            
            return False
        except Exception as e:
            logger.error(f"Failed to create README from page: {e}")
            return False
    
    def try_get_old_versions(self, plugin_url: str, plugin_data: Dict, old_versions_dir: Path, plugin_name: str):
        """Пытается получить старые версии плагина"""
        try:
            # Получаем информацию о версиях
            versions = plugin_data.get("versions", [])
            if not versions or len(versions) <= 1:
                return False
            
            # Пропускаем первую версию (она уже загружена как текущая)
            for i, version_info in enumerate(versions[1:], 1):
                version = version_info.get("version")
                if not version:
                    continue
                
                download_url = version_info.get("download_url")
                if not download_url:
                    continue
                
                # Создаем имя файла для версии
                version_filename = f"{plugin_name}_v{version}.cs"
                version_path = old_versions_dir / version_filename
                
                # Загружаем версию
                self.download_plugin_code(download_url, version_path)
                
                # Ограничиваем количество старых версий
                if i >= 5:  # Ограничиваем 5 старыми версиями
                    break
            
            return True
        except Exception as e:
            logger.error(f"Failed to get old versions: {e}")
            return False
    
    def download_documentation(self, url: str, target_path: Path) -> bool:
        """Загружает документацию"""
        try:
            # Добавляем случайную задержку
            time.sleep(random.uniform(1, 3))
            
            logger.debug(f"Downloading documentation from {url}")
            
            # Используем stream=True для постепенной загрузки
            with self.session.get(url, stream=True) as response:
                response.raise_for_status()
                
                # Открываем файл в бинарном режиме для записи
                with open(target_path, 'wb') as f:
                    # Загружаем и записываем данные частями
                    for chunk in response.iter_content(chunk_size=8192):
                        if chunk:  # фильтруем keep-alive новые куски
                            f.write(chunk)
            
            logger.info(f"Downloaded documentation to {target_path} ({target_path.stat().st_size} bytes)")
            return True
        except Exception as e:
            logger.error(f"Failed to download documentation from {url}: {e}")
            return False
    
    def create_readme_from_json(self, plugin_data: Dict, plugin_dir: Path):
        """Создает README.md на основе данных из JSON файла плагина"""
        try:
            readme_path = plugin_dir / "README.md"
            
            # Если README уже существует, пропускаем
            if readme_path.exists():
                return True
            
            name = plugin_data.get("name", "Unknown Plugin")
            description = plugin_data.get("description", "")
            author = plugin_data.get("author", "Unknown")
            
            # Категории могут быть строкой или списком
            categories = plugin_data.get("categories", [])
            if isinstance(categories, str):
                categories = categories.split(",")
            
            # Формируем URL на основе имеющихся данных
            plugin_id = plugin_data.get("id", "")
            url = plugin_data.get("url", "")
            if not url and plugin_id:
                url = f"https://umod.org/plugins/{plugin_id}"
            
            # Получаем информацию о версиях
            latest_version = plugin_data.get("latest_version", "")
            
            with open(readme_path, 'w', encoding='utf-8') as f:
                f.write(f"# {name}\n\n")
                
                if url:
                    f.write(f"Plugin URL: {url}\n\n")
                
                f.write(f"Author: {author}\n\n")
                
                if latest_version:
                    f.write(f"Latest Version: {latest_version}\n\n")
                
                if categories and len(categories) > 0:
                    f.write("Categories: " + ", ".join(categories) + "\n\n")
                
                if description:
                    f.write("## Description\n\n")
                    f.write(f"{description}\n")
            
            logger.info(f"Created README.md from JSON data")
            return True
        except Exception as e:
            logger.error(f"Failed to create README from JSON: {e}")
            return False

def main():
    # Создаем экземпляр класса и запускаем организацию плагинов
    organizer = PluginOrganizer(max_workers=3)
    
    try:
        # Обрабатываем все плагины (без ограничения)
        organizer.organize_plugins()
    except Exception as e:
        logger.error(f"Error during organization: {e}")

if __name__ == "__main__":
    main() 