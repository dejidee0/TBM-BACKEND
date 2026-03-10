(function () {
  "use strict";

  var brandName = "TBM BUILDING AI VISUALIZER API";
  var consoleName = "API Experience Console";
  var frontendUrl = "https://tdm-web.vercel.app/";
  var openApiUrl = "/swagger/v1/swagger.json";
  var logoUrl = "/swagger-ui/tbm-logo.svg";

  function decorateTopbar() {
    var link = document.querySelector(".swagger-ui .topbar .link");
    if (!link || document.getElementById("tbm-brand")) {
      return;
    }

    link.innerHTML = "";
    link.classList.add("tbm-brand-link");

    var brandWrap = document.createElement("div");
    brandWrap.id = "tbm-brand";
    brandWrap.className = "tbm-brand";

    var logo = document.createElement("img");
    logo.className = "tbm-brand__logo";
    logo.src = logoUrl;
    logo.alt = "TBM";
    logo.loading = "lazy";

    var text = document.createElement("span");
    text.className = "tbm-brand__text";

    var title = document.createElement("span");
    title.className = "tbm-brand__title";
    title.textContent = "TBM API";

    var subtitle = document.createElement("span");
    subtitle.className = "tbm-brand__sub";
    subtitle.textContent = "Construction + Interior Platform";

    text.appendChild(title);
    text.appendChild(subtitle);
    brandWrap.appendChild(logo);
    brandWrap.appendChild(text);
    link.appendChild(brandWrap);
  }

  function decorateInfoPanel() {
    var docTitle = document.querySelector(".information-container .info .title");
    if (docTitle) {
      docTitle.textContent = brandName + " Platform API";
    }

    if (document.title !== brandName + " - Swagger") {
      document.title = brandName + " - Swagger";
    }

    var info = document.querySelector(".information-container .info");
    if (!info) {
      return;
    }

    var banner = document.getElementById("tbm-swagger-panel");
    if (banner) {
      return;
    }

    banner = document.createElement("div");
    banner.id = "tbm-swagger-panel";

    var headline = document.createElement("strong");
    headline.textContent = consoleName;

    var sub = document.createElement("span");
    sub.textContent = "Frontend-aligned contract for auth, commerce, vendor operations, admin analytics, and AI workflows.";

    var links = document.createElement("div");
    links.className = "tbm-panel-links";

    var frontendLink = document.createElement("a");
    frontendLink.href = frontendUrl;
    frontendLink.target = "_blank";
    frontendLink.rel = "noreferrer noopener";
    frontendLink.textContent = "Frontend";

    var openApiLink = document.createElement("a");
    openApiLink.href = openApiUrl;
    openApiLink.target = "_blank";
    openApiLink.rel = "noreferrer noopener";
    openApiLink.textContent = "OpenAPI JSON";

    banner.appendChild(headline);
    banner.appendChild(sub);
    links.appendChild(frontendLink);
    links.appendChild(openApiLink);
    banner.appendChild(links);
    info.insertBefore(banner, info.firstChild);
  }

  function applyBranding() {
    decorateTopbar();
    decorateInfoPanel();
  }

  var attempts = 0;
  var maxAttempts = 18;
  var timer = setInterval(function () {
    applyBranding();
    attempts += 1;
    if (attempts >= maxAttempts) {
      clearInterval(timer);
    }
  }, 250);

  window.addEventListener("load", function () {
    applyBranding();
    setTimeout(applyBranding, 250);
    setTimeout(applyBranding, 900);
  });
})();
