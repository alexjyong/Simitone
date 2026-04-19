/* Simitone Website — main.js
   Minimal interactivity: smooth scroll, mobile nav toggle */

document.addEventListener('DOMContentLoaded', function () {
    // ---- Mobile Navigation Toggle ----
    var navToggle = document.querySelector('.nav-toggle');
    var navLinks = document.querySelector('.nav-links');

    if (navToggle && navLinks) {
        navToggle.addEventListener('click', function () {
            var isOpen = navLinks.classList.toggle('active');
            navToggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });

        // Close mobile nav when a link is clicked
        var links = navLinks.querySelectorAll('a');
        links.forEach(function (link) {
            link.addEventListener('click', function () {
                navLinks.classList.remove('active');
                navToggle.setAttribute('aria-expanded', 'false');
            });
        });

        // Close mobile nav when clicking outside
        document.addEventListener('click', function (e) {
            if (!navToggle.contains(e.target) && !navLinks.contains(e.target)) {
                navLinks.classList.remove('active');
                navToggle.setAttribute('aria-expanded', 'false');
            }
        });
    }

    // ---- Smooth Scroll for Anchor Links (fallback for older browsers) ----
    var anchors = document.querySelectorAll('a[href^="#"]');
    anchors.forEach(function (anchor) {
        anchor.addEventListener('click', function (e) {
            var targetId = this.getAttribute('href');
            if (targetId === '#') return;
            var target = document.querySelector(targetId);
            if (target) {
                e.preventDefault();
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });

    // ---- Lightbox ----
    var lightbox = document.getElementById('lightbox');
    var lightboxImg = lightbox ? lightbox.querySelector('.lightbox-img') : null;
    var lightboxCaption = lightbox ? lightbox.querySelector('.lightbox-caption') : null;
    var galleryItems = document.querySelectorAll('.gallery-item');

    function openLightbox(fullSrc, altText, caption) {
        if (!lightbox || !lightboxImg) return;
        lightboxImg.src = fullSrc;
        lightboxImg.alt = altText;
        if (lightboxCaption) lightboxCaption.textContent = caption;
        lightbox.removeAttribute('hidden');
        document.body.style.overflow = 'hidden';
    }

    function closeLightbox() {
        if (!lightbox) return;
        lightbox.setAttribute('hidden', '');
        if (lightboxImg) lightboxImg.src = '';
        document.body.style.overflow = '';
    }

    galleryItems.forEach(function (item) {
        item.addEventListener('click', function () {
            var fullSrc = this.getAttribute('data-full');
            var alt = this.getAttribute('data-alt');
            var caption = this.querySelector('.gallery-caption');
            var captionText = caption ? caption.textContent : alt;
            openLightbox(fullSrc, alt, captionText);
        });

        item.setAttribute('tabindex', '0');
        item.setAttribute('role', 'button');
        item.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                this.click();
            }
        });
    });

    if (lightbox) {
        var closeBtn = lightbox.querySelector('.lightbox-close');
        var closeTriggers = lightbox.querySelectorAll('[data-lightbox-close]');

        if (closeBtn) closeBtn.addEventListener('click', closeLightbox);
        closeTriggers.forEach(function (el) {
            el.addEventListener('click', closeLightbox);
        });

        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && !lightbox.hasAttribute('hidden')) {
                closeLightbox();
            }
        });
    }

    // ---- Version Badge (fetches latest release from GitHub API) ----
    var versionBadge = document.getElementById('version-badge');
    if (versionBadge) {
        fetch('https://api.github.com/repos/alexjyong/simitone/releases/latest')
            .then(function (res) {
                if (!res.ok) throw new Error('API error');
                return res.json();
            })
            .then(function (data) {
                if (data.tag_name) {
                    var tagEl = versionBadge.querySelector('.version-tag');
                    if (tagEl) tagEl.textContent = data.tag_name;
                    versionBadge.removeAttribute('hidden');
                }
            })
            .catch(function () {
                // Silently hide badge if API fails
                if (versionBadge) versionBadge.setAttribute('hidden', '');
            });
    }
});
