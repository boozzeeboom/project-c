import json, subprocess
# T-Q22: Create 2 test quests (additive). Use direct YAML approach via write_file for assets
# (safer than AssetDatabase.CreateAsset через Roslyn).
import os, subprocess

# 1) Create ItemData YAML вручную (безопасно — текстовый формат)
item_yaml = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 009489055a12f1d4fb6aee9664b1af28, type: 3}
  m_Name: Item_Resource_TestStageItem
  m_EditorClassIdentifier:
  itemName: TestStageItem
  itemType: 0
  maxStack: 99
  icon: {fileID: 0}
  description: ""
  basePrice: 0
  weight: 0
"""
item_path = "C:/UNITY_PROJECTS/ProjectC_client/Assets/_Project/Resources/Items/Item_Resource_TestStageItem.asset"
if not os.path.exists(item_path):
    with open(item_path, 'w', encoding='utf-8') as f:
        f.write(item_yaml)
    print("Created:", item_path)
else:
    print("Exists:", item_path)
