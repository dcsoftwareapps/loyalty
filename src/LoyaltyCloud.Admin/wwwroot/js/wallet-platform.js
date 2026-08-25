window.loyaltyCloudWallet = window.loyaltyCloudWallet || {};

window.loyaltyCloudWallet.getPlatformSignal = () => ({
    userAgent: navigator.userAgent || "",
    platform: navigator.platform || "",
    vendor: navigator.vendor || "",
    maxTouchPoints: navigator.maxTouchPoints || 0
});


window.loyaltyCloudWallet.navigateToGoogleWallet = (saveUrl) => {
    const url = new URL(saveUrl);
    if (url.protocol !== "https:" ||
        url.hostname !== "pay.google.com" ||
        !url.pathname.startsWith("/gp/v/save/")) {
        throw new Error("Invalid Google Wallet save URL.");
    }

    window.location.assign(url.href);
};
