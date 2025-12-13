#!/bin/bash
# Generates code coverage reports for SonarQube analysis
# This script runs tests with code coverage collection and generates reports in OpenCover XML format
# that can be consumed by SonarQube for code coverage analysis.

set -e

# Default parameters
OUTPUT_DIR="${1:-./TestResults}"
FORMAT="${2:-opencover}"

echo "========================================"
echo "Code Coverage Report Generation"
echo "========================================"
echo ""

# Ensure output directory exists
if [ ! -d "$OUTPUT_DIR" ]; then
    mkdir -p "$OUTPUT_DIR"
    echo "Created output directory: $OUTPUT_DIR"
fi

# Clean previous coverage results
echo "Cleaning previous coverage results..."
find "$OUTPUT_DIR" -name "coverage.*" -type f -delete 2>/dev/null || true
echo "Previous results cleaned."
echo ""

# Run tests with coverage
echo "Running tests with code coverage collection..."
echo ""

COVERAGE_FILE="$OUTPUT_DIR/coverage.$FORMAT.xml"

dotnet test Squirrel.Wiki.Core.Tests/Squirrel.Wiki.Core.Tests.csproj \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat="$FORMAT" \
    /p:CoverletOutput="../$COVERAGE_FILE" \
    /p:ExcludeByFile="**/*Designer.cs" \
    /p:Exclude="[xunit.*]*,[*.Tests]*" \
    --verbosity minimal

echo ""
echo "========================================"
echo "Coverage Report Generated Successfully!"
echo "========================================"
echo ""
echo "Coverage file location:"
echo "  $COVERAGE_FILE"
echo ""
echo "To use with SonarQube, add this to your sonar-project.properties:"
echo "  sonar.cs.opencover.reportsPaths=$COVERAGE_FILE"
echo ""
echo "Or pass it as a parameter to the SonarScanner:"
echo "  dotnet sonarscanner begin /k:\"project-key\" /d:sonar.cs.opencover.reportsPaths=\"$COVERAGE_FILE\""
echo ""
