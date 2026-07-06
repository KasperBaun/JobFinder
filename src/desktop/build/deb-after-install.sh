#!/bin/bash
# Custom deb post-install (overrides electron-builder's default template). It is a superset of
# that template — same /usr/bin symlink + mime/desktop-db refresh — with one change: it ALWAYS
# marks chrome-sandbox SUID (chmod 4755) instead of probing `unshare --user`.
#
# Why: Ubuntu 24.04 ships kernel.apparmor_restrict_unprivileged_userns=1, which blocks the
# user-namespace sandbox for apps under /opt. The default template's probe runs as root at
# install time (where userns always works), so it wrongly picks 0755 and the app aborts with
# SIGTRAP at runtime ("chrome-sandbox ... must be owned by root and have mode 4755"). The SUID
# sandbox does not rely on unprivileged userns, so it works regardless of the apparmor restriction.
#
# Paths are literal because productName (Jobfinder -> /opt/Jobfinder) and the package/executable
# name (jobfinder-desktop) are fixed in package.json / electron-builder.yml.

if type update-alternatives 2>/dev/null >&1; then
    # Remove a previous non-alternatives link if present.
    if [ -L '/usr/bin/jobfinder-desktop' -a -e '/usr/bin/jobfinder-desktop' -a "`readlink '/usr/bin/jobfinder-desktop'`" != '/etc/alternatives/jobfinder-desktop' ]; then
        rm -f '/usr/bin/jobfinder-desktop'
    fi
    update-alternatives --install '/usr/bin/jobfinder-desktop' 'jobfinder-desktop' '/opt/Jobfinder/jobfinder-desktop' 100 || ln -sf '/opt/Jobfinder/jobfinder-desktop' '/usr/bin/jobfinder-desktop'
else
    ln -sf '/opt/Jobfinder/jobfinder-desktop' '/usr/bin/jobfinder-desktop'
fi

# Always use the SUID chrome-sandbox (see header). dpkg installs files as root, so ownership is
# already root:root; we only need the setuid bit.
chmod 4755 '/opt/Jobfinder/chrome-sandbox' || true

if hash update-mime-database 2>/dev/null; then
    update-mime-database /usr/share/mime || true
fi

if hash update-desktop-database 2>/dev/null; then
    update-desktop-database /usr/share/applications || true
fi
