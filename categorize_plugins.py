import os
import shutil
import re
from pathlib import Path
import logging
from collections import defaultdict

# Настройка логирования
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class PluginCategorizer:
    """Класс для категоризации плагинов по алфавиту, чтобы избежать ограничения GitHub на отображение файлов"""
    
    def __init__(self, source_dir="plugins", output_dir="categorized_plugins", max_plugins_per_category=250):
        self.source_dir = Path(source_dir)
        self.output_dir = Path(output_dir)
        self.max_plugins_per_category = max_plugins_per_category
        
        # Убедимся, что выходная директория существует
        self.output_dir.mkdir(parents=True, exist_ok=True)
    
    def categorize_by_alphabet(self):
        """Распределяет плагины по алфавитным категориям"""
        # Получаем список всех плагинов
        plugin_dirs = [d for d in self.source_dir.iterdir() if d.is_dir()]
        total_plugins = len(plugin_dirs)
        
        logger.info(f"Found {total_plugins} plugins to categorize")
        
        # Сортируем плагины по имени
        plugin_dirs.sort(key=lambda x: x.name.lower())
        
        # Определяем, сколько категорий нам нужно
        num_categories = (total_plugins + self.max_plugins_per_category - 1) // self.max_plugins_per_category
        
        # Определяем категории по первой букве
        categories = defaultdict(list)
        
        for plugin_dir in plugin_dirs:
            # Берём первую букву названия плагина в верхнем регистре
            first_letter = plugin_dir.name[0].upper()
            categories[first_letter].append(plugin_dir)
        
        # Объединяем маленькие категории
        categories_list = self.optimize_categories(categories)
        
        # Создаем категории и копируем файлы
        for category_name, plugins in categories_list.items():
            category_dir = self.output_dir / category_name
            category_dir.mkdir(exist_ok=True)
            
            logger.info(f"Creating category {category_name} with {len(plugins)} plugins")
            
            for plugin_dir in plugins:
                # Копируем директорию плагина в категорию
                dest_dir = category_dir / plugin_dir.name
                if dest_dir.exists():
                    shutil.rmtree(dest_dir)
                shutil.copytree(plugin_dir, dest_dir)
                
        logger.info(f"Categorization complete. Created {len(categories_list)} categories.")
        return categories_list
    
    def optimize_categories(self, categories):
        """Оптимизирует категории, объединяя маленькие"""
        # Сначала создаем список категорий и их размеров
        category_sizes = [(letter, len(plugins)) for letter, plugins in categories.items()]
        category_sizes.sort(key=lambda x: x[0])  # Сортируем по букве
        
        optimized = defaultdict(list)
        current_category = ""
        current_size = 0
        
        for letter, size in category_sizes:
            # Если текущая категория пуста или добавление не превысит лимит
            if current_size == 0 or current_size + size <= self.max_plugins_per_category:
                if current_category == "":
                    current_category = letter
                else:
                    # Если это не первая буква в категории, добавляем дефис
                    current_category = f"{current_category}-{letter}"
                
                # Добавляем плагины в оптимизированную категорию
                optimized[current_category].extend(categories[letter])
                current_size += size
            else:
                # Начинаем новую категорию
                current_category = letter
                current_size = size
                optimized[current_category].extend(categories[letter])
        
        # Проверяем и разделяем слишком большие категории
        final_categories = defaultdict(list)
        
        for category_name, plugins in optimized.items():
            if len(plugins) > self.max_plugins_per_category:
                # Разделяем большую категорию на подкатегории
                chunks = self.split_into_chunks(plugins, self.max_plugins_per_category)
                for i, chunk in enumerate(chunks, 1):
                    if len(chunks) > 1:
                        sub_category = f"{category_name}-{i}"
                    else:
                        sub_category = category_name
                    final_categories[sub_category] = chunk
            else:
                final_categories[category_name] = plugins
        
        return final_categories
    
    def split_into_chunks(self, items, max_chunk_size):
        """Разделяет список на части заданного размера"""
        return [items[i:i + max_chunk_size] for i in range(0, len(items), max_chunk_size)]
    
    def categorize_by_prefix(self):
        """Распределяет плагины по общим префиксам (например, Zone*, Admin*, etc.)"""
        # Получаем список всех плагинов
        plugin_dirs = [d for d in self.source_dir.iterdir() if d.is_dir()]
        
        # Группируем плагины по префиксам
        prefix_groups = defaultdict(list)
        no_prefix_group = []
        
        for plugin_dir in plugin_dirs:
            # Ищем префикс (все символы до первой заглавной буквы после первой)
            prefix_match = re.match(r'^([A-Z][a-z]+)', plugin_dir.name)
            if prefix_match:
                prefix = prefix_match.group(1)
                prefix_groups[prefix].append(plugin_dir)
            else:
                no_prefix_group.append(plugin_dir)
        
        # Создаем категории для префиксов с достаточным количеством плагинов
        categories = defaultdict(list)
        min_plugins_for_category = 5  # Минимальное количество плагинов для создания отдельной категории
        
        for prefix, plugins in prefix_groups.items():
            if len(plugins) >= min_plugins_for_category:
                categories[prefix] = plugins
            else:
                # Добавляем маленькие группы в категорию Misc
                no_prefix_group.extend(plugins)
        
        # Добавляем оставшиеся плагины в смешанные категории по алфавиту
        if no_prefix_group:
            no_prefix_dict = defaultdict(list)
            for plugin_dir in no_prefix_group:
                first_letter = plugin_dir.name[0].upper()
                no_prefix_dict[first_letter].append(plugin_dir)
            
            # Оптимизируем категории без префикса
            optimized_no_prefix = self.optimize_categories(no_prefix_dict)
            for key, value in optimized_no_prefix.items():
                categories[f"Other-{key}"] = value
        
        # Создаем категории и копируем файлы
        for category_name, plugins in categories.items():
            category_dir = self.output_dir / category_name
            category_dir.mkdir(exist_ok=True)
            
            logger.info(f"Creating category {category_name} with {len(plugins)} plugins")
            
            for plugin_dir in plugins:
                # Копируем директорию плагина в категорию
                dest_dir = category_dir / plugin_dir.name
                if dest_dir.exists():
                    shutil.rmtree(dest_dir)
                shutil.copytree(plugin_dir, dest_dir)
        
        logger.info(f"Categorization complete. Created {len(categories)} categories.")
        return categories

def main():
    # Создаем экземпляр класса и запускаем категоризацию
    categorizer = PluginCategorizer()
    
    # Выберите метод категоризации:
    # 1. По алфавиту:
    # categories = categorizer.categorize_by_alphabet()
    
    # 2. По префиксам:
    categories = categorizer.categorize_by_prefix()
    
    # Выводим результаты
    print("\nКатегории плагинов:")
    for category, plugins in categories.items():
        print(f"{category}: {len(plugins)} плагинов")

if __name__ == "__main__":
    main() 