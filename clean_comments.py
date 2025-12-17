import os
import re

def remove_comments(text):
    # Pattern to match:
    # 1. Double quoted strings: "[^"\\]*(?:\\.[^"\\]*)*"
    # 2. Verbatim strings (C#): @(?:""|[^"])*" 
    #    (Simplified verbatim: @"..." where quotes are doubled. This is tricky. 
    #     Let's handle standard strings and simple verbatim if possible, or just standard.
    #     Standard regex for strings is usually enough for most cases.)
    # 3. Single quoted strings: '[^'\\]*(?:\\.[^'\\]*)*'
    # 4. Block comments: /\*[\s\S]*?\*/
    # 5. Line comments: //.*
    
    # We need to be careful with C# verbatim strings @"...". make sure we don't treat // inside them as comments.
    # Verbatim strings start with @", contain anything, and end with ". Quote is escaped as "".
    
    # Combined pattern
    pattern = r'(@"(?:""|[^"])*")|("[^"\\]*(?:\\.[^"\\]*)*")|(\'[^\'\\]*(?:\\.[^\'\\]*)*\')|(/\*[\s\S]*?\*/)|(//.*)'
    
    def replacer(match):
        # If any string group matches, preserve it
        if match.group(1): return match.group(1) # Verbatim
        if match.group(2): return match.group(2) # Double quote
        if match.group(3): return match.group(3) # Single quote
        
        # If it's a comment, return garbage (space) or empty.
        # Check if it was a block or line comment
        if match.group(4): # Block comment
            return ' ' 
        if match.group(5): # Line comment
            return ' '
        return match.group(0)

    try:
        return re.sub(pattern, replacer, text)
    except Exception as e:
        print(f"Error in regex: {e}")
        return text

def clean_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()

        new_content = remove_comments(content)
        
        # Handle empty lines: reduce multiple empty lines to one
        lines = [line.rstrip() for line in new_content.splitlines()]
        clean_lines = []
        last_empty = False
        for line in lines:
            if not line:
                if not last_empty:
                    clean_lines.append(line)
                    last_empty = True
            else:
                clean_lines.append(line)
                last_empty = False
        
        new_content = '\n'.join(clean_lines)

        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Cleaned: {filepath}")
    except Exception as e:
        print(f"Error processing {filepath}: {e}")

def main():
    directory = "."
    extensions = ['.cs', '.cshtml', '.js', '.css', '.html', '.razor']
    
    for root, dirs, files in os.walk(directory):
        if 'bin' in dirs: dirs.remove('bin')
        if 'obj' in dirs: dirs.remove('obj')
        if '.git' in dirs: dirs.remove('.git')
        
        for file in files:
            if any(file.lower().endswith(ext) for ext in extensions):
                filepath = os.path.join(root, file)
                clean_file(filepath)

if __name__ == "__main__":
    main()
