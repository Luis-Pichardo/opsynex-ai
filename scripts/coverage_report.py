#!/usr/bin/env python3
"""Cobertura de líneas del backend combinando los reportes Cobertura de todas las pruebas.

Une las líneas cubiertas de cada archivo en todos los `coverage.cobertura.xml` (pruebas
unitarias y de integración) y calcula el porcentaje por ensamblado. Con `--min Ensamblado=80`
termina con error si no se alcanza el mínimo (lo usa la CI).

Uso:
    dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults/cov
    python3 scripts/coverage_report.py TestResults/cov --min Opsynex.Domain=80 --min Opsynex.Application=80
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path


def collect(root: Path) -> dict[str, dict[str, dict[int, bool]]]:
    """{ensamblado: {archivo: {línea: cubierta}}} uniendo todos los reportes."""
    result: dict[str, dict[str, dict[int, bool]]] = defaultdict(lambda: defaultdict(dict))
    for report in root.rglob("coverage.cobertura.xml"):
        for package in ET.parse(report).getroot().iter("package"):
            name = package.get("name", "")
            for cls in package.iter("class"):
                lines = result[name][cls.get("filename", "")]
                for line in cls.iter("line"):
                    number = int(line.get("number", "0"))
                    lines[number] = lines.get(number, False) or int(line.get("hits", "0")) > 0
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--min", action="append", default=[], metavar="ENSAMBLADO=PORCENTAJE")
    args = parser.parse_args()

    data = collect(args.directory)
    if not data:
        print(f"No se encontraron reportes coverage.cobertura.xml en {args.directory}", file=sys.stderr)
        return 1

    rates: dict[str, float] = {}
    print(f"{'Ensamblado':<28} {'Líneas':>8} {'Cubiertas':>10} {'%':>7}")
    for name in sorted(data):
        total = sum(len(lines) for lines in data[name].values())
        covered = sum(sum(lines.values()) for lines in data[name].values())
        rates[name] = 100.0 * covered / total if total else 100.0
        print(f"{name:<28} {total:>8} {covered:>10} {rates[name]:>6.1f}%")

    failed = False
    for rule in args.min:
        name, _, minimum = rule.partition("=")
        rate = rates.get(name)
        if rate is None or rate < float(minimum):
            print(f"✗ {name}: {rate if rate is not None else 0:.1f}% < {minimum}%", file=sys.stderr)
            failed = True
        else:
            print(f"✓ {name}: {rate:.1f}% ≥ {minimum}%")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
