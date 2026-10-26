document.addEventListener('DOMContentLoaded', () => {
  initYear();
  initGitHubReleases();
  initTabbedCarousel();
});

function initYear() {
  const yearEl = document.getElementById('currentYear');
  if (yearEl) {
    yearEl.textContent = new Date().getFullYear();
  }
}

async function initGitHubReleases() {
  const repoOwner = 'juniodevs';
  const repoName = 'TrashAll';
  const releasesApi = `https://api.github.com/repos/${repoOwner}/${repoName}/releases/latest`;

  const heroVersionLabel = document.getElementById('heroVersionLabel');
  const heroDownloadBtn = document.getElementById('heroDownloadBtn');
  const btnDownloadPortable = document.getElementById('btnDownloadPortable');
  const btnDownloadDependent = document.getElementById('btnDownloadDependent');

  try {
    const res = await fetch(releasesApi);
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const data = await res.json();

    const tagName = data.tag_name || 'v1.0.0';
    const assets = data.assets || [];

    const portableAsset = assets.find(a => a.name.toLowerCase().includes('portable') && a.name.endsWith('.zip'));
    const dependentAsset = assets.find(a => a.name.toLowerCase().includes('dependent') && a.name.endsWith('.zip'));

    const formatSize = (bytes) => {
      if (!bytes) return '';
      const mb = (bytes / (1024 * 1024)).toFixed(1);
      return ` (${mb} MB)`;
    };

    if (heroVersionLabel) {
      const sizeStr = portableAsset ? formatSize(portableAsset.size) : '';
      heroVersionLabel.textContent = `Versão ${tagName}${sizeStr} • 100% Portátil x64`;
    }

    if (portableAsset && portableAsset.browser_download_url) {
      if (heroDownloadBtn) heroDownloadBtn.href = portableAsset.browser_download_url;
      if (btnDownloadPortable) btnDownloadPortable.href = portableAsset.browser_download_url;
    }

    if (dependentAsset && dependentAsset.browser_download_url) {
      if (btnDownloadDependent) btnDownloadDependent.href = dependentAsset.browser_download_url;
    }

  } catch (err) {
    console.warn('Usando fallback padrão para release:', err);
    if (heroVersionLabel) {
      heroVersionLabel.textContent = 'Versão Portátil x64 • Grátis';
    }
  }
}

function initTabbedCarousel() {
  const slides = document.querySelectorAll('.carousel-slide');
  const tabs = document.querySelectorAll('.btn-tab, .tab-btn');
  const titleEl = document.getElementById('carouselWindowTitle');
  const subtitleEl = document.getElementById('carouselSubtitle');

  if (!slides.length) return;

  let currentIndex = 0;

  function showSlide(index) {
    if (index < 0) index = slides.length - 1;
    if (index >= slides.length) index = 0;
    currentIndex = index;

    slides.forEach((slide, i) => {
      slide.classList.toggle('active', i === currentIndex);
    });

    tabs.forEach((tab, i) => {
      tab.classList.toggle('active', i === currentIndex);
    });

    const activeSlide = slides[currentIndex];
    if (activeSlide) {
      const title = activeSlide.getAttribute('data-title');
      const sub = activeSlide.getAttribute('data-sub');

      if (titleEl && title) titleEl.textContent = title;
      if (subtitleEl && sub) subtitleEl.textContent = sub;
    }
  }

  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      const idx = parseInt(tab.getAttribute('data-index'), 10);
      if (!isNaN(idx)) showSlide(idx);
    });
  });

  let timer = setInterval(() => {
    showSlide(currentIndex + 1);
  }, 6000);

  const container = document.querySelector('.screenshot-display-frame, .carousel-container');
  if (container) {
    container.addEventListener('mouseenter', () => clearInterval(timer));
    container.addEventListener('mouseleave', () => {
      timer = setInterval(() => showSlide(currentIndex + 1), 6000);
    });
  }
}
