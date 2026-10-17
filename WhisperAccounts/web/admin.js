"use strict";
const status = document.getElementById("status");
function csrfToken() {
  const part = document.cookie.split("; ").find(x => x.startsWith("__Host-whisper_csrf=") || x.startsWith("whisper_csrf_dev="));
  return part ? decodeURIComponent(part.slice(part.indexOf("=") + 1)) : "";
}
async function api(path, body) {
  const response = await fetch(path, { credentials: "same-origin", method: body ? "POST" : "GET",
    headers: body ? { "Content-Type": "application/json", "X-CSRF-Token": csrfToken() } : {},
    body: body ? JSON.stringify(body) : undefined });
  const result = await response.json();
  if (!response.ok) throw new Error(result.message || "Request failed. Sign in again.");
  return result;
}
async function refresh() {
  const me = await api("/api/me");
  status.textContent = `${me.email}: ${me.entitlement.status}`;
  document.getElementById("logout").hidden = false;
  document.getElementById("google-login").hidden = true;
  document.getElementById("owner").hidden = !me.is_owner;
  document.getElementById("desktop").hidden = !new URLSearchParams(location.search).get("desktop");
  if (!me.is_owner) return;
  const result = await api("/api/grants");
  const table = document.getElementById("grants");
  table.replaceChildren();
  for (const grant of result.grants) {
    const row = document.createElement("tr");
    for (const value of [grant.email, grant.kind, grant.active ? (grant.subject ? "Active" : "Pending verified sign-in") : "Revoked"]) {
      const cell = document.createElement("td"); cell.textContent = value; row.append(cell);
    }
    const cell = document.createElement("td");
    if (grant.kind !== "founder_lifetime" && grant.active) {
      const button = document.createElement("button"); button.textContent = "Revoke";
      button.addEventListener("click", async () => {
        if (!confirm(`Revoke free access for ${grant.email}?`)) return;
        try { await api("/api/grants/revoke", {email: grant.email}); await refresh(); }
        catch (error) { status.textContent = error.message; }
      }); cell.append(button);
    } else if (grant.kind === "founder_lifetime") { cell.textContent = "Lifetime protected"; }
    row.append(cell); table.append(row);
  }
}
async function signIn() {
  const challenge = await api("/auth/challenge");
  google.accounts.id.initialize({client_id: challenge.client_id, nonce: challenge.nonce, auto_select: false,
    callback: async result => {
      try { await api("/auth/google", {credential: result.credential}); await refresh(); }
      catch (error) { status.textContent = error.message; }
    }});
  google.accounts.id.renderButton(document.getElementById("google-login"), {theme: "outline", size: "large"});
}
document.getElementById("grant-form").addEventListener("submit", async event => {
  event.preventDefault();
  try { await api("/api/grants", {email: document.getElementById("email").value}); await refresh(); }
  catch (error) { status.textContent = error.message; }
});
document.getElementById("logout").addEventListener("click", async () => {
  try { await api("/auth/logout", {}); location.reload(); } catch (error) { status.textContent = error.message; }
});
window.addEventListener("load", async () => {
  try { await refresh(); } catch { status.textContent = "Sign in to continue."; try { await signIn(); } catch (error) { status.textContent = error.message; } }
});
document.getElementById("desktop-form").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    await api("/api/desktop/approve", {request_id: new URLSearchParams(location.search).get("desktop"),
      display_code: document.getElementById("desktop-code").value.toUpperCase()});
    status.textContent = "Desktop approved. Return to your Windows app.";
    document.getElementById("desktop").hidden = true;
    history.replaceState(null, "", "/admin");
  } catch (error) { status.textContent = error.message; }
});
