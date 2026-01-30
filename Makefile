.PHONY: help build release publish cli

PROJECT = Jubeka.CLI/Jubeka.CLI.csproj
CONFIG ?= Release
RUNTIME ?= linux-x64
PUBLISH_DIR ?= ./out

help:
	@echo "Targets:"
	@echo "  project   Build the CLI project (Debug)"
	@echo "  release   Publish Release binary (single-file)"
	@echo "  publish   Publish with custom vars"
	@echo ""
	@echo "Examples:"
	@echo "\tmake release"
	@echo "\tmake release RUNTIME=linux-arm64"
	@echo "\tmake publish PROJECT=Jubeka.CLI/Jubeka.CLI.csproj RUNTIME=linux-x64 PUBLISH_DIR=./out"

build:
	dotnet build $(PROJECT) -c Debug -r $(RUNTIME) -o $(PUBLISH_DIR)

release:
	dotnet publish $(PROJECT) -c $(CONFIG) -r $(RUNTIME) \
		/p:PublishSingleFile=true /p:SelfContained=true \
		/p:DebugType=None /p:DebugSymbols=false \
		/p:PublishTrimmed=false -o $(PUBLISH_DIR)

publish:
	dotnet publish $(PROJECT) -c $(CONFIG) -r $(RUNTIME) -o $(PUBLISH_DIR)
