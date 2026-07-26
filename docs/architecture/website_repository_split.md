# BentoDesk Website Repository Split

Date: 2026-07-07

The BentoDesk website source has been moved out of the public application repository.

Public entry:

- Website: https://github.com/TCOTC/BentoDesk/

Repository ownership:

- `BentoDesk` public repository: Windows app source, installer scripts, tests, public README, changelog, and app architecture docs.
- Private website repository: website source, website screenshots, SEO/GEO files, website deployment scripts, static update manifest, and website analytics pages.

Release boundary:

- App code, installer, and tests should be committed in the public `BentoDesk` repository.
- Website content, `llms.txt`, `sitemap.xml`, `robots.txt`, download page content, and `public/update/stable.json` should be updated in the private website repository.
- Do not re-add `bentodesk-site/` to this repository.

When release workflow changes, update the private website repository documentation and the BentoDesk release skill together.
