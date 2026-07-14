# Attribution and upstream references

## KasaTapoClient

Copyright (c) 2026 Neil Colvin.

KasaTapoClient is an independent .NET implementation for TP-Link Kasa and Tapo devices.

## Upstream behavioral and compatibility references

This repository uses the public `python-kasa` project as an upstream reference for protocol behavior, device support expectations, compatibility validation, and documentation cross-checking.

- Project: `python-kasa`
- Repository: https://github.com/python-kasa/python-kasa
- Public documentation: https://python-kasa.readthedocs.io/

## Upstream license and copyright notice

The upstream `python-kasa` project carries its own copyright and license terms.
Those terms apply to that project and its source distribution, not automatically to this repository.

At the time of local release preparation, the upstream repository includes GPLv3 licensing text and project-level copyright notices in its root `LICENSE` file. No source code from `python-kasa` has been copied into this repository; source file header comments referencing `python-kasa` describe independently-written code whose behavior was modeled after the upstream project for protocol/compatibility purposes only. If code is copied directly from upstream in future changes, the licensing impact must be reviewed before release.

## Referenced support and documentation material

This repository may reference upstream public documentation and supported-device information when describing compatibility expectations or behavioral parity.
Those references are informational and attributional.

## Trademark notice

TP-Link, Kasa, and Tapo are trademarks of their respective owners. This project is an independent, unofficial .NET library and is not affiliated with or endorsed by TP-Link.
