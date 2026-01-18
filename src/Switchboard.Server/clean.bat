@echo off

del /s /f /q logs\*.*
rmdir /s /q logs

del /f /q sb.json
del /f /q *.db

@echo on
