"""Archive an explicitly supplied local fixture; this command performs no network access."""

from argparse import ArgumentParser
from pathlib import Path

from .contract import archive_payload, serialize_manifest


def main() -> None:
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True, help="Local input fixture")
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--source-id", required=True)
    parser.add_argument("--requested-uri", required=True)
    parser.add_argument("--parser-version", required=True)
    args = parser.parse_args()

    manifest = archive_payload(
        args.input.read_bytes(),
        args.output_root,
        source_id=args.source_id,
        requested_uri=args.requested_uri,
        parser_version=args.parser_version,
        extension=args.input.suffix or ".bin",
    )
    print(serialize_manifest(manifest))


if __name__ == "__main__":
    main()
