window.loyaltyCloudWallet = window.loyaltyCloudWallet || {};

window.loyaltyCloudWallet.getPlatformSignal = () => ({
    userAgent: navigator.userAgent || "",
    platform: navigator.platform || "",
    vendor: navigator.vendor || "",
    maxTouchPoints: navigator.maxTouchPoints || 0
});
