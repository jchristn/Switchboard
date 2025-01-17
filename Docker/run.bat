@echo off

IF "%1" == "" GOTO :Usage

if not exist sb.json (
  echo Configuration file sb.json not found.
  exit /b 1
)

REM Items that require persistence
REM   sb.json
REM   logs/

REM Argument order matters!

docker run ^
  -p 8000:8000 ^
  -t ^
  -i ^
  -e "TERM=xterm-256color" ^
  -v .\sb.json:/app/sb.json ^
  -v .\logs\:/app/logs/ ^
  jchristn/switchboard:%1

GOTO :Done

:Usage
ECHO Provide one argument indicating the tag. 
ECHO Example: dockerrun.bat v2.0.0
:Done
@echo on
