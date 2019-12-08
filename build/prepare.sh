#!/bin/bash

LONG_TAG=$(git describe --long)
VER_MAJORMINOR=$(echo $LONG_TAG | sed -r 's|^v?([0-9.]+)-[0-9]+-g[0-9a-f]+$|\1|g')
VER_MAJOR=$(echo $VER_MAJORMINOR | sed -r 's|^([0-9]+)\.[0-9]+$|\1|g')
VER_COMMITS_SINCE_TAG=$(echo $LONG_TAG | sed -r 's|^v?[0-9.]+-([0-9]+)-g[0-9a-f]+$|\1|g')

if [ "$CI_COMMIT_REF_NAME" = "master" ]; then
    FLAVOR=Release
    BRANCH_SUFFIX=
else
    FLAVOR=Debug
    BRANCH_SUFFIX=$CI_COMMIT_REF_NAME
fi

VERSION_BASE=$VER_MAJORMINOR.$VER_COMMITS_SINCE_TAG
VERSION_ASSEMBLY=$VER_MAJOR.0.0.0
VERSION_PRODUCT=$VERSION_BASE.0
