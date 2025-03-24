import os
import shutil
from pathlib import Path
import logging

# Logging setup
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class AlphabeticalCategorizer:
    """Class for simple categorization of plugins by the first letter of their name"""
    
    def __init__(self, source_dir="plugins", output_dir="alpha_plugins"):
        self.source_dir = Path(source_dir)
        self.output_dir = Path(output_dir)
        
        # Make sure the output directory exists
        self.output_dir.mkdir(parents=True, exist_ok=True)
    
    def categorize(self):
        """Distributes plugins into alphabetical categories (by first letter)"""
        # Get a list of all plugins
        plugin_dirs = [d for d in self.source_dir.iterdir() if d.is_dir()]
        total_plugins = len(plugin_dirs)
        
        logger.info(f"Found {total_plugins} plugins to categorize")
        
        # Create categories by first letter
        categories = {}
        
        for plugin_dir in plugin_dirs:
            # Take the first letter of the plugin name in uppercase
            first_letter = plugin_dir.name[0].upper()
            
            # Add the plugin to the appropriate category
            if first_letter not in categories:
                categories[first_letter] = []
            categories[first_letter].append(plugin_dir)
        
        # Create category directories and copy files
        for letter, plugins in categories.items():
            category_dir = self.output_dir / letter
            category_dir.mkdir(exist_ok=True)
            
            logger.info(f"Creating category {letter} with {len(plugins)} plugins")
            
            for plugin_dir in plugins:
                # Copy the plugin directory to the category
                dest_dir = category_dir / plugin_dir.name
                if dest_dir.exists():
                    shutil.rmtree(dest_dir)
                shutil.copytree(plugin_dir, dest_dir)
        
        logger.info(f"Categorization complete. Created {len(categories)} categories.")
        
        # Output statistics by category
        print("\nPlugin categories:")
        for letter, plugins in sorted(categories.items()):
            print(f"{letter}: {len(plugins)} plugins")
        
        return categories

def main():
    # Create a class instance and run categorization
    categorizer = AlphabeticalCategorizer()
    categories = categorizer.categorize()

if __name__ == "__main__":
    main() 