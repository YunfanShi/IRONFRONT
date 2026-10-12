#!/bin/bash
cd "$(dirname "$0")" || exit 1
echo "IRONFRONT"
echo "1. Dedicated server"
echo "2. Host and play on this Mac"
read -r -p "Choose mode [1/2]: " mode
case "$mode" in
  1) node scripts/start-lan.cjs --mode dedicated ;;
  2) node scripts/start-lan.cjs --mode host ;;
  *) echo "Please choose 1 or 2."; exit 1 ;;
esac
