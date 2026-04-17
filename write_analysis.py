import codecs  
content = open("test.py", "rb").read().decode("utf-8")  
codecs.open("docs/world/LargeScaleMMO/ARTIFACT_DEEP_ANALYSIS_2026-04-17.md", "w", "utf-8").write(content) 
